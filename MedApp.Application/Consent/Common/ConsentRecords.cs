using FluentValidation;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Consent.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Consent;
using MedApp.Domain.Attachments;
using MedApp.Domain.Consent;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Consent.Common;

/// <summary>Shared steps of the consent handlers: loading, the scan's checks, the response, and validation rules.</summary>
public static class ConsentRecords
{
    public static async Task<ConsentRecord> GetForUpdateOrThrowAsync(this IConsentRepository repository, Guid id, CancellationToken ct) =>
        await repository.GetForUpdateAsync(id, ct) ?? throw new NotFoundException($"Consent record '{id}' was not found.");

    public static async Task<ConsentRecordResponse> ToResponseAsync(
        ConsentRecord record,
        IIdentityReadService identity,
        PathwayActor viewer,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var usernames = await identity.GetUsernamesByIdAsync(ConsentMapper.PeopleIn(record), ct);
        return ConsentMapper.ToResponse(record, usernames, viewer, nowUtc);
    }

    /// <summary>
    /// The file to be a record's scan: a 400 on <c>ScanAttachmentId</c> unless it's a live file on the pathway (or one
    /// of its stages) and no other record's scan.
    /// </summary>
    public static async Task<Attachment> CheckScanAsync(
        IConsentRepository repository, Guid attachmentId, PatientPathway pathway, string field, CancellationToken ct)
    {
        var attachment = await repository.GetAttachmentForUpdateAsync(attachmentId, ct);
        if (attachment is null || !ConsentWorkflow.IsOnPathway(attachment, pathway))
            throw new ValidationException([new FluentValidation.Results.ValidationFailure(field, ConsentWorkflow.ScanNotFoundMessage)]);
        if (await repository.IsScanAsync(attachmentId, ct))
            throw new ValidationException([new FluentValidation.Results.ValidationFailure(field, ConsentWorkflow.ScanInUseMessage)]);
        return attachment;
    }

    /// <summary>
    /// Soft-deletes a record's scan with its correction (decision 71), with an <c>AttachmentDeleted</c> audit entry, to
    /// save with the record. Nothing if it has none, or it's already deleted.
    /// </summary>
    public static async Task<PatientPathwayAuditEntry?> DeleteScanAsync(
        IConsentRepository repository, ConsentRecord record, PathwayActor actor, DateTime nowUtc, CancellationToken ct)
    {
        if (record.ScanAttachmentId is not { } scanId
            || await repository.GetAttachmentForUpdateAsync(scanId, ct) is not { IsDeleted: false } scan)
            return null;

        scan.DeletedAtUtc = nowUtc;
        scan.DeletedByUserId = actor.UserId;
        scan.DeletedByUsername = actor.Username;
        var stage = scan.StageId is { } stageId ? record.Pathway.Stages.FirstOrDefault(s => s.Id == stageId) : null;
        var target = new AttachmentTarget(scan.PatientId, record.Pathway, stage);
        // After the record's own entry, so the timeline shows the correction first.
        return AttachmentAudit.Entry(scan, target, actor, PatientPathwayAuditAction.AttachmentDeleted, nowUtc.AddTicks(1));
    }

    /// <summary>A detail that must be given (withdrawn, entered in error), at most 500 characters.</summary>
    public static IRuleBuilderOptions<T, string?> RequiredDetail<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(d => !string.IsNullOrWhiteSpace(d)).WithMessage("Say what happened.")
            .MaximumLength(ConsentRecord.MaxDetailLength)
            .WithMessage($"The detail must be at most {ConsentRecord.MaxDetailLength} characters.");

    public static IRuleBuilderOptions<T, int> ExpectedVersion<T>(this IRuleBuilder<T, int> rule) =>
        rule.GreaterThan(0).WithMessage("Give the version of the consent record you changed.");
}
