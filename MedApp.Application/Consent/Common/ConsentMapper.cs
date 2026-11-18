using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Consent;
using MedApp.Domain.Consent;

namespace MedApp.Application.Consent.Common;

/// <summary>Consent forms and records to their responses, with what the viewer may do.</summary>
public static class ConsentMapper
{
    public static ConsentFormResponse ToResponse(ConsentForm form, bool inUse) => new()
    {
        Id = form.Id,
        Name = form.Name,
        FormVersion = form.FormVersion,
        IsArchived = form.IsArchived,
        CanBeDeleted = !inUse,
        CreatedAt = form.CreatedAt,
        LastUpdated = form.LastUpdated,
    };

    /// <summary>Everyone a record names, for one username lookup.</summary>
    public static IEnumerable<Guid> PeopleIn(ConsentRecord record) =>
        record.StatusChangedByUserId is { } changedBy ? [record.RecordedByUserId, changedBy] : [record.RecordedByUserId];

    /// <summary>
    /// A record's response. <c>record.Pathway</c> (with its stages) and <c>record.Patient</c> must be loaded; the
    /// <c>Can...</c> flags are for <paramref name="viewer"/>.
    /// </summary>
    public static ConsentRecordResponse ToResponse(
        ConsentRecord record,
        IReadOnlyDictionary<Guid, string> usernames,
        PathwayActor viewer,
        DateTime nowUtc)
    {
        var stage = record.Pathway?.Stages.FirstOrDefault(s => s.Id == record.StageId);
        return new ConsentRecordResponse
        {
            Id = record.Id,
            PatientId = record.PatientId,
            PathwayId = record.PathwayId,
            StageId = record.StageId,
            StageName = stage?.Name ?? string.Empty,
            ConsentFormId = record.ConsentFormId,
            FormName = record.FormName,
            FormVersion = record.FormVersion,
            SignedOn = record.SignedOn,
            SignedBy = record.SignedBy.ToString(),
            RecordedByUserId = record.RecordedByUserId,
            RecordedByUsername = usernames.GetValueOrDefault(record.RecordedByUserId),
            RecordedAtUtc = record.RecordedAtUtc,
            ScanAttachmentId = record.ScanAttachmentId,
            Status = record.Status.ToString(),
            WithdrawnOn = record.WithdrawnOn,
            StatusDetail = record.StatusDetail,
            StatusChangedByUsername = record.StatusChangedByUserId is { } by ? usernames.GetValueOrDefault(by) : null,
            StatusChangedAtUtc = record.StatusChangedAtUtc,
            WithdrawnAfterStageFinished = ConsentWorkflow.WithdrawnAfterStageFinished(record, stage),
            Version = record.Version,
            CanWithdraw = viewer.Has(Permission.RecordConsent) && ConsentWorkflow.WhyCantWithdraw(record) is null,
            CanMarkEnteredInError = ConsentWorkflow.MayMarkEnteredInError(record, viewer, nowUtc)
                                    && ConsentWorkflow.WhyCantMarkEnteredInError(record) is null,
            CanAttachScan = viewer.Has(Permission.RecordConsent) && ConsentWorkflow.WhyCantAttachScan(record) is null,
        };
    }
}
