using System.Buffers;
using System.Security.Cryptography;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Attachments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Attachments.Responses;
using MedApp.Domain.Attachments;
using MedApp.Domain.Pathways.PatientPathways;
using MediatR;

namespace MedApp.Application.Attachments.Commands.UploadAttachment;

/// <summary>
/// Attaches a file. In order: storage is set up and the file isn't declared too large; what it's attached to exists,
/// its patient's record can be changed (not restricted or anonymised: <see cref="PatientRules"/>), and it allows new
/// files; the body is copied to a temporary file (<see cref="IUploadStaging"/>), counting its size and
/// hashing it as it goes, so 20 MB is never held in memory; its type is found from its contents
/// (<see cref="FileTypeSniffer"/>, at most two at a time: <see cref="FileWorkLimiter"/>); it's scanned for malware;
/// what it's attached to is checked again, since the upload may have taken a minute (still there, the record still
/// changeable, the stage or pathway still taking files); it's stored, with a thumbnail if it's an image; then its
/// record and audit entry are saved together. If anything after storing fails, the stored bytes are deleted again,
/// within <see cref="CleanUpTimeout"/>.
/// </summary>
public sealed class UploadAttachmentHandler(
    IAttachmentRepository attachments,
    IPatientRepository patients,
    IPatientPathwayRepository pathways,
    IFileStore fileStore,
    IUploadStaging staging,
    IMalwareScanner scanner,
    IThumbnailGenerator thumbnails,
    TimeProvider timeProvider)
    : IRequestHandler<UploadAttachmentCommand, AttachmentResponse>
{
    private const int BufferSize = 81920;

    /// <summary>How long deleting a failed upload's bytes may take before it's left for later.</summary>
    public static readonly TimeSpan CleanUpTimeout = TimeSpan.FromSeconds(10);

    public async Task<AttachmentResponse> Handle(UploadAttachmentCommand request, CancellationToken cancellationToken)
    {
        if (!fileStore.IsConfigured)
            throw new FileStorageUnavailableException();
        if (request.ContentLength > AttachmentLimits.MaxFileBytes)
            throw new FileTooLargeException();

        await CheckTargetAsync(request, cancellationToken);

        var fileName = AttachmentFileName.Clean(request.FileName)
                       ?? throw new ArgumentException("The validator checks the file has a name.", nameof(request));

        await using var staged = staging.Create();
        var (size, sha256) = await StageAsync(request.Content, staged, cancellationToken);
        var type = await FileWorkLimiter.RunAsync(() => FileTypeSniffer.Identify(staged, fileName), cancellationToken);

        staged.Position = 0;
        var scanStatus = await scanner.ScanAsync(staged, cancellationToken);
        if (scanStatus == AttachmentScanStatus.Infected)
            throw new MalwareDetectedException();

        // Read again, fresh: the record may have been restricted, anonymised or deleted, or the stage finished, while
        // the file arrived. This narrows the window to the time it takes to store the file.
        var target = await CheckTargetAsync(request, cancellationToken);

        var id = Guid.NewGuid();
        var hasThumbnail = false;
        try
        {
            staged.Position = 0;
            await fileStore.SaveAsync(AttachmentStorageKeys.File(id), staged, type.ContentType, cancellationToken);
            hasThumbnail = await SaveThumbnailAsync(id, staged, type, cancellationToken);

            var attachment = new Attachment
            {
                Id = id,
                PatientId = target.PatientId,
                PathwayId = target.Pathway?.Id,
                StageId = target.Stage?.Id,
                ReferralId = target.Referral?.Id,
                FileName = fileName,
                ContentType = type.ContentType,
                SizeBytes = size,
                Sha256 = sha256,
                ScanStatus = scanStatus,
                HasThumbnail = hasThumbnail,
                UploadedByUserId = request.Actor.UserId,
                UploadedByUsername = request.Actor.Username,
                UploadedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            };

            var audit = AttachmentAudit.Entry(
                attachment, target, request.Actor, PatientPathwayAuditAction.AttachmentAdded, attachment.UploadedAtUtc);
            await attachments.AddAsync(attachment, audit, cancellationToken);

            return AttachmentMapper.ToResponse(attachment, target, request.Actor.UserId, request.CanCorrectRecords, canAttachFiles: true);
        }
        catch
        {
            // Leave no bytes that no record points to. Not with the request's token: it may be why this failed.
            await DeleteQuietlyAsync(AttachmentStorageKeys.File(id));
            if (hasThumbnail)
                await DeleteQuietlyAsync(AttachmentStorageKeys.Thumbnail(id));
            throw;
        }
    }

    /// <summary>
    /// What the file is attached to, or a 404 if it's gone, or a 409 if the patient's record can't change
    /// (<see cref="PatientRules"/>) or it doesn't take files now.
    /// </summary>
    private async Task<AttachmentTarget> CheckTargetAsync(UploadAttachmentCommand request, CancellationToken ct)
    {
        var target = await AttachmentTargets.ResolveAsync(
            request.PatientId, request.PathwayId, request.StageId, request.ReferralId, patients, pathways, attachments, ct);
        PatientRules.EnsureCanChange(target.Owner);
        if (target.WhyFilesCantChange() is { } reason)
            throw new ConflictException(reason);
        return target;
    }

    /// <summary>
    /// Copies the body to <paramref name="staged"/>, refusing it as soon as it passes the limit (whatever its declared
    /// length said), and returns its size and SHA-256.
    /// </summary>
    private static async Task<(long Size, string Sha256)> StageAsync(Stream body, Stream staged, CancellationToken ct)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            long size = 0;
            int read;
            while ((read = await body.ReadAsync(buffer.AsMemory(0, BufferSize), ct)) > 0)
            {
                size += read;
                if (size > AttachmentLimits.MaxFileBytes)
                    throw new FileTooLargeException();

                hash.AppendData(buffer, 0, read);
                await staged.WriteAsync(buffer.AsMemory(0, read), ct);
            }

            await staged.FlushAsync(ct);
            return (size, Convert.ToHexStringLower(hash.GetHashAndReset()));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Makes and stores a thumbnail of an image, if it can; whether it did.</summary>
    private async Task<bool> SaveThumbnailAsync(Guid id, Stream staged, AttachmentFileType type, CancellationToken ct)
    {
        if (!type.CanHaveThumbnail)
            return false;

        staged.Position = 0;
        var thumbnail = await thumbnails.CreateAsync(staged, type, ct);
        if (thumbnail is null)
            return false;

        await using var content = new MemoryStream(thumbnail, writable: false);
        await fileStore.SaveAsync(
            AttachmentStorageKeys.Thumbnail(id), content, IThumbnailGenerator.ContentTypeOf(type), ct);
        return true;
    }

    private async Task DeleteQuietlyAsync(string key)
    {
        // Not with the request's token (it may be why this failed), but not for ever either.
        using var timeout = new CancellationTokenSource(CleanUpTimeout);
        try
        {
            await fileStore.DeleteAsync(key, timeout.Token);
        }
        catch (Exception)
        {
            // Best effort: an orphaned blob wastes space but exposes nothing (only the API reads the store).
        }
    }
}
