using System.Buffers;
using System.Security.Cryptography;
using MedApp.Domain.Attachments;

namespace MedApp.Application.Attachments.Common;

/// <summary>A file taken in and stored, before its record is saved: what its record holds.</summary>
public sealed record StoredFile(
    Guid Id,
    string FileName,
    AttachmentFileType Type,
    long Size,
    string Sha256,
    AttachmentScanStatus ScanStatus,
    bool HasThumbnail);

/// <summary>
/// Takes in an uploaded file, the same way for every upload (an attachment, a specimen's report): the body is copied to
/// a temporary file (<see cref="IUploadStaging"/>), counting its size and hashing it as it goes, so 20 MB is never held
/// in memory; its type is found from its contents (<see cref="FileTypeSniffer"/>, at most two at a time:
/// <see cref="FileWorkLimiter"/>); it's scanned for malware; the caller checks what it's attached to again; it's
/// stored, with a thumbnail if it's an image; then the caller saves its record. If anything after storing fails, the
/// stored bytes are deleted again, within <see cref="CleanUpTimeout"/>.
/// </summary>
public sealed class AttachmentIntake(
    IFileStore fileStore,
    IUploadStaging staging,
    IMalwareScanner scanner,
    IThumbnailGenerator thumbnails)
{
    private const int BufferSize = 81920;

    /// <summary>How long deleting a failed upload's bytes may take before it's left for later.</summary>
    public static readonly TimeSpan CleanUpTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Stages, identifies, scans and stores <paramref name="content"/> as <paramref name="fileName"/> (already
    /// cleaned), calling <paramref name="checkAgain"/> before storing it and <paramref name="save"/> after, to save its
    /// record. Throws <see cref="FileTooLargeException"/>, an unsupported type's exception, or
    /// <see cref="MalwareDetectedException"/>.
    /// </summary>
    public async Task<T> TakeInAsync<T>(
        string fileName,
        Stream content,
        Func<CancellationToken, Task> checkAgain,
        Func<StoredFile, Task<T>> save,
        CancellationToken ct)
    {
        await using var staged = staging.Create();
        var (size, sha256) = await StageAsync(content, staged, ct);
        var type = await FileWorkLimiter.RunAsync(() => FileTypeSniffer.Identify(staged, fileName), ct);

        staged.Position = 0;
        var scanStatus = await scanner.ScanAsync(staged, ct);
        if (scanStatus == AttachmentScanStatus.Infected)
            throw new MalwareDetectedException();

        // Read again, fresh, by the caller: what it's attached to may have changed while the file arrived. This narrows
        // the window to the time it takes to store the file.
        await checkAgain(ct);

        var id = Guid.NewGuid();
        var hasThumbnail = false;
        try
        {
            staged.Position = 0;
            await fileStore.SaveAsync(AttachmentStorageKeys.File(id), staged, type.ContentType, ct);
            hasThumbnail = await SaveThumbnailAsync(id, staged, type, ct);

            return await save(new StoredFile(id, fileName, type, size, sha256, scanStatus, hasThumbnail));
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
