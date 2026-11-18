using MedApp.Application.Attachments.Common;
using MedApp.Application.Attachments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Letters.Repositories;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Specimens.Common;
using MedApp.Application.Specimens.Repositories;
using MedApp.Domain.Pathways.PatientPathways;
using MediatR;

namespace MedApp.Application.Attachments.Commands.DeleteAttachment;

public sealed class DeleteAttachmentHandler(
    IAttachmentRepository attachments,
    IPatientRepository patients,
    IPatientPathwayRepository pathways,
    ILetterRepository letters,
    ISpecimenRepository specimens,
    MedApp.Application.Results.Repositories.IResultRepository results,
    TimeProvider timeProvider)
    : IRequestHandler<DeleteAttachmentCommand>
{
    public const string LetterPdfMessage =
        "This file is a letter's approved PDF, so it can't be deleted on its own. If the letter was wrong, withdraw it from its page.";

    public async Task Handle(DeleteAttachmentCommand request, CancellationToken cancellationToken)
    {
        var attachment = await attachments.GetForUpdateAsync(request.Id, cancellationToken);
        if (attachment is null || attachment.IsDeleted)
            throw new NotFoundException($"File '{request.Id}' was not found.");

        if (!AttachmentMapper.CanDelete(attachment, request.Actor.UserId, request.CanCorrectRecords, canAttachFiles: true))
            throw new ForbiddenException("Only the person who attached a file, or an admin, can delete it.");

        // A letter's approved PDF is the record of what was sent (decision 71): withdraw the letter instead.
        if (await letters.IsPdfAsync(attachment.Id, cancellationToken))
            throw new ConflictException(LetterPdfMessage);

        var target = await AttachmentTargets.OfAsync(attachment, patients, pathways, attachments, cancellationToken);
        // A restricted or anonymised record can't change, for admins too (an admin lifts the restriction first).
        PatientRules.EnsureCanChange(target.Owner);
        if (!request.CanCorrectRecords && target.WhyFilesCantChange() is { } reason)
            throw new ConflictException(reason);

        // A specimen's report goes only with the specimen's own correction, never on its own (decision 71).
        if (await specimens.IsReportAsync(attachment.Id, cancellationToken))
            throw new ConflictException(SpecimenWorkflow.ReportedFileMessage);

        // So does a result's report (an external one): mark the result entered in error instead.
        if (await results.IsReportAsync(attachment.Id, cancellationToken))
            throw new ConflictException(MedApp.Application.Results.Common.ResultWorkflow.ReportFileMessage);

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        attachment.DeletedAtUtc = nowUtc;
        attachment.DeletedByUserId = request.Actor.UserId;
        attachment.DeletedByUsername = request.Actor.Username;

        var audit = AttachmentAudit.Entry(
            attachment, target, request.Actor, PatientPathwayAuditAction.AttachmentDeleted, nowUtc);
        await attachments.SaveAsync(audit, cancellationToken);
    }
}
