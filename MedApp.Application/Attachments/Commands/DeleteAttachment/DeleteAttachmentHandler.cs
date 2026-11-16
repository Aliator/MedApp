using MedApp.Application.Attachments.Common;
using MedApp.Application.Attachments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using MediatR;

namespace MedApp.Application.Attachments.Commands.DeleteAttachment;

public sealed class DeleteAttachmentHandler(
    IAttachmentRepository attachments,
    IPatientProcedureRepository procedures,
    TimeProvider timeProvider)
    : IRequestHandler<DeleteAttachmentCommand>
{
    public async Task Handle(DeleteAttachmentCommand request, CancellationToken cancellationToken)
    {
        var attachment = await attachments.GetForUpdateAsync(request.Id, cancellationToken);
        if (attachment is null || attachment.IsDeleted)
            throw new NotFoundException($"File '{request.Id}' was not found.");

        if (!AttachmentMapper.CanDelete(attachment, request.Actor.UserId, request.IsAdmin))
            throw new ForbiddenException("Only the person who attached a file, or an admin, can delete it.");

        var target = await AttachmentTargets.OfAsync(attachment, procedures, cancellationToken);
        if (!request.IsAdmin && target.WhyFilesCantChange() is { } reason)
            throw new ConflictException(reason);

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        attachment.DeletedAtUtc = nowUtc;
        attachment.DeletedByUserId = request.Actor.UserId;
        attachment.DeletedByUsername = request.Actor.Username;

        var audit = AttachmentAudit.Entry(
            attachment, target, request.Actor, PatientProcedureAuditAction.AttachmentDeleted, nowUtc);
        await attachments.SaveAsync(audit, cancellationToken);
    }
}
