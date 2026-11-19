using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Domain.Attachments;
using MedApp.Domain.Consent;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Consent.Common;

/// <summary>
/// The rules of a consent record (<c>docs/centre-phases-3-5-plan.md</c>, decisions 145 to 147). Handlers check here
/// before changing one, and the response's <c>Can...</c> flags come from the same rules.
/// <list type="bullet">
/// <item><b>Recording</b> needs RecordConsent, a record that can change (decision 75: a restricted one can't), an open
/// pathway and a stage still to do (neither finished nor skipped: consent is often taken ahead of its stage).</item>
/// <item><b>Withdrawing</b> (RecordConsent) and <b>entered in error</b> (whoever recorded it within 24 hours, or
/// CorrectRecords) work in any pathway status, and while the record is restricted (they protect the patient), never
/// once it's anonymised.</item>
/// <item><b>MedApp doesn't assess consent</b> (decision 147): not capacity, validity, or whether the form fits. It records
/// what the clinician states. A withdrawal after the stage finished is flagged; nothing changes by itself.</item>
/// </list>
/// </summary>
public static class ConsentWorkflow
{
    /// <summary>How far back the signed date may be.</summary>
    public const int MaxSignedDaysBack = 365;

    /// <summary>How long whoever recorded it may mark it entered in error themselves (CorrectRecords any time).</summary>
    public static readonly TimeSpan OwnCorrectionWindow = TimeSpan.FromHours(24);

    public const string ChangedMeanwhileMessage =
        "Someone else changed this consent record while you had it open. Reload the pathway to see their change, then try again.";

    public const string RestrictedMessage =
        "This patient's record is restricted, so consent can't be recorded for them until an admin lifts the restriction. " +
        "Ask an admin what to do.";

    public const string ScanFileMessage =
        "This file is a consent form's scan, so it can't be deleted on its own. If the consent was recorded in error, mark it entered in error.";

    public const string EnteredInErrorNotAllowedMessage =
        "Only whoever recorded this consent can mark it entered in error, within 24 hours of recording it. After that, ask someone who can correct records.";

    public const string ScanNotFoundMessage =
        "That file isn't attached to this pathway (or it's been deleted). Attach the scan to the pathway first.";

    public const string ScanInUseMessage = "That file is already another consent record's scan.";

    /// <summary>A status in words: "valid", "withdrawn", "entered in error".</summary>
    public static string Describe(ConsentStatus status) => status switch
    {
        ConsentStatus.Valid => "valid",
        ConsentStatus.Withdrawn => "withdrawn",
        ConsentStatus.EnteredInError => "entered in error",
        _ => status.ToString()
    };

    /// <summary>A change must start from the version saved now, or someone else has changed it since (409).</summary>
    public static void EnsureVersion(ConsentRecord record, int expectedVersion)
    {
        if (record.Version != expectedVersion)
            throw new ConflictException(ChangedMeanwhileMessage);
    }

    /// <summary>
    /// Why consent can't be recorded for this stage now, or null if it can: the record can change, the pathway is open
    /// and the stage is still to do.
    /// </summary>
    public static string? WhyCantRecord(PatientPathway pathway, PatientPathwayStage stage)
    {
        if (PatientRules.IsAnonymised(pathway.Patient))
            return PatientRules.AnonymisedMessage;
        if (PatientRules.IsRestricted(pathway.Patient))
            return RestrictedMessage;
        if (!PathwayWorkflow.IsOpen(pathway.Status))
            return $"Consent can't be recorded while the pathway is {PathwayWorkflow.Describe(pathway.Status)}.";
        if (stage.IsSkipped)
            return "Consent can't be recorded for a skipped stage.";
        return stage.IsCompleted ? "Consent can't be recorded for a stage that's already finished." : null;
    }

    /// <summary>Why it can't be withdrawn now, or null: it's Valid and the record isn't anonymised (restricted is fine).</summary>
    public static string? WhyCantWithdraw(ConsentRecord record)
    {
        if (PatientRules.IsAnonymised(record.Patient))
            return PatientRules.AnonymisedMessage;
        return record.Status == ConsentStatus.Valid
            ? null
            : $"This consent has been {Describe(record.Status)}, so it can't be withdrawn.";
    }

    /// <summary>Why it can't be marked entered in error now, or null: not already, and the record isn't anonymised.</summary>
    public static string? WhyCantMarkEnteredInError(ConsentRecord record)
    {
        if (PatientRules.IsAnonymised(record.Patient))
            return PatientRules.AnonymisedMessage;
        return record.Status == ConsentStatus.EnteredInError
            ? "This consent record has already been marked entered in error."
            : null;
    }

    /// <summary>Why a scan can't be attached now, or null: it has none, it's Valid, and the record can change.</summary>
    public static string? WhyCantAttachScan(ConsentRecord record)
    {
        if (PatientRules.WhyCantChange(record.Patient) is { } why)
            return why;
        if (record.ScanAttachmentId is not null)
            return "This consent record already has its scan.";
        return record.Status == ConsentStatus.Valid
            ? null
            : $"This consent has been {Describe(record.Status)}, so a scan can't be added.";
    }

    /// <summary>
    /// Whether <paramref name="actor"/> may mark it entered in error: a holder of CorrectRecords at any time, or whoever
    /// recorded it, while they still hold RecordConsent, within <see cref="OwnCorrectionWindow"/>.
    /// </summary>
    public static bool MayMarkEnteredInError(ConsentRecord record, PathwayActor actor, DateTime nowUtc) =>
        actor.Has(Permission.CorrectRecords)
        || (actor.UserId == record.RecordedByUserId
            && actor.Has(Permission.RecordConsent)
            && nowUtc < record.RecordedAtUtc + OwnCorrectionWindow);

    /// <summary>
    /// Why the signed date can't be used, or null: not after today (UK) and at most <see cref="MaxSignedDaysBack"/> days
    /// back.
    /// </summary>
    public static string? WhySignedOnInvalid(DateOnly signedOn, DateOnly today)
    {
        if (signedOn > today)
            return "The date it was signed can't be in the future.";
        return signedOn < today.AddDays(-MaxSignedDaysBack) ? "The date it was signed can be at most a year ago." : null;
    }

    /// <summary>Why the withdrawal date can't be used, or null: not after today, and not before it was signed.</summary>
    public static string? WhyWithdrawnOnInvalid(ConsentRecord record, DateOnly withdrawnOn, DateOnly today)
    {
        if (withdrawnOn > today)
            return "The date it was withdrawn can't be in the future.";
        return withdrawnOn < record.SignedOn ? "The date it was withdrawn can't be before the date it was signed." : null;
    }

    /// <summary>
    /// Whether a file can be a record's scan: live, the same patient's, and on the record's pathway or one of its stages.
    /// </summary>
    public static bool IsOnPathway(Attachment attachment, PatientPathway pathway) =>
        !attachment.IsDeleted
        && attachment.PatientId == pathway.PatientId
        && (attachment.PathwayId == pathway.Id
            || (attachment.StageId is { } stageId && pathway.Stages.Any(s => s.Id == stageId)));

    /// <summary>Whether the consent was withdrawn after its stage was finished (decision 147: flagged, nothing more).</summary>
    public static bool WithdrawnAfterStageFinished(ConsentRecord record, PatientPathwayStage? stage) =>
        record.Status == ConsentStatus.Withdrawn
        && stage?.CompletedAtUtc is { } finishedAt
        && record.StatusChangedAtUtc is { } withdrawnAt
        && withdrawnAt > finishedAt;
}
