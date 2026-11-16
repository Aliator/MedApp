using MedApp.Application.Attachments.Common;
using MedApp.Application.Attachments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using MediatR;

namespace MedApp.Application.Attachments.Commands.DownloadAttachment;

public sealed class DownloadAttachmentHandler(
    IAttachmentRepository attachments,
    IPatientProcedureRepository procedures,
    IFileStore fileStore,
    TimeProvider timeProvider)
    : IRequestHandler<DownloadAttachmentCommand, AttachmentDownload>
{
    public async Task<AttachmentDownload> Handle(DownloadAttachmentCommand request, CancellationToken cancellationToken)
    {
        if (!fileStore.IsConfigured)
            throw new FileStorageUnavailableException();

        var attachment = await attachments.GetByIdAsync(request.Id, cancellationToken);
        if (attachment is null || attachment.IsDeleted)
            throw new NotFoundException($"File '{request.Id}' was not found.");

        var content = await fileStore.OpenReadAsync(AttachmentStorageKeys.File(attachment.Id), cancellationToken)
                      ?? throw new NotFoundException($"File '{request.Id}' has no contents in storage.");
        try
        {
            var target = await AttachmentTargets.OfAsync(attachment, procedures, cancellationToken);
            var audit = AttachmentAudit.Entry(
                attachment,
                target,
                request.Actor,
                PatientProcedureAuditAction.AttachmentDownloaded,
                timeProvider.GetUtcNow().UtcDateTime);
            await attachments.AddAuditAsync(audit, cancellationToken);

            var type = AttachmentFileTypes.FromContentType(attachment.ContentType);
            return new AttachmentDownload(
                AttachmentFileName.ForDownload(attachment.FileName, type),
                attachment.ContentType,
                attachment.SizeBytes,
                content);
        }
        catch
        {
            await content.DisposeAsync();
            throw;
        }
    }
}
