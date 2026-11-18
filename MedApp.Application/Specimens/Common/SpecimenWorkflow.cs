using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Specimens;
using static MedApp.Domain.Specimens.SpecimenStatus;

namespace MedApp.Application.Specimens.Common;

/// <summary>
/// The rules of a specimen's life (<c>docs/centre-phases-3-5-plan.md</c>, decisions 81 to 90). Handlers check here
/// before changing a specimen, and the response's <c>Can...</c> flags come from the same rules.
/// <list type="bullet">
/// <item>Statuses move forward only: Taken, Sent, Received, Reported; or, from any open one, Lost or EnteredInError,
/// and from Sent or Received, RejectedByLab. Every closed status is final: taking another is a new specimen.</item>
/// <item>Taking needs the stage to be editable (the current one, or one an admin unlocked) and the pathway open (not on
/// hold or closed). The lab's steps work in any pathway status, so a report is never stuck.</item>
/// <item>Taken, sent and received times are now or typed up to 7 days back, never in the future and never before the
/// step before; reported is now.</item>
/// <item>A restricted record (decision 75): a specimen already taken can still go on (sent, received, rejected,
/// reported, lost) or be marked entered in error, as care that protects the patient must finish; a new one can't be
/// taken, nor details edited. An anonymised record's can't change at all.</item>
/// </list>
/// MedApp tracks and shows specimens; it never reads or interprets a report. Turnaround and overdue are date
/// arithmetic on the times staff record and the type's expected days.
/// </summary>
public static class SpecimenWorkflow
{
    /// <summary>How far back a step's time may be typed.</summary>
    public static readonly TimeSpan MaxBackdating = TimeSpan.FromDays(7);

    /// <summary>How long whoever recorded a specimen may mark it entered in error themselves (CorrectRecords any time).</summary>
    public static readonly TimeSpan OwnCorrectionWindow = TimeSpan.FromHours(24);

    /// <summary>The lists' Rejected and lost view goes back this far, and Reported this far.</summary>
    public static readonly TimeSpan RejectedAndLostWindow = TimeSpan.FromDays(30);

    public static readonly TimeSpan ReportedWindow = TimeSpan.FromDays(7);

    /// <summary>Still to come back from the lab.</summary>
    public static readonly IReadOnlyList<SpecimenStatus> OpenStatuses = [Taken, Sent, Received];

    public const string ChangedMeanwhileMessage =
        "Someone else changed this specimen while you had it open. Reload it to see their change, then try again.";

    public const string RestrictedTakeMessage =
        "This patient's record is restricted, so a specimen can't be recorded for them until an admin lifts the restriction. " +
        "Ask an admin what to do with the specimen.";

    public const string ReportedFileMessage =
        "This file is a specimen's report, so it can't be deleted on its own.";

    public static bool IsOpen(SpecimenStatus status) => OpenStatuses.Contains(status);

    /// <summary>A status in words, for messages and the timeline: "sent", "rejected by the lab".</summary>
    public static string Describe(SpecimenStatus status) => status switch
    {
        Taken => "taken",
        Sent => "sent",
        Received => "received",
        Reported => "reported",
        RejectedByLab => "rejected by the lab",
        Lost => "lost",
        EnteredInError => "entered in error",
        _ => status.ToString()
    };

    /// <summary>A rejection reason in words: "Insufficient sample".</summary>
    public static string Describe(SpecimenRejectionReason reason) => reason switch
    {
        SpecimenRejectionReason.Unlabelled => "Unlabelled",
        SpecimenRejectionReason.Mislabelled => "Mislabelled",
        SpecimenRejectionReason.Leaked => "Leaked",
        SpecimenRejectionReason.InsufficientSample => "Insufficient sample",
        SpecimenRejectionReason.WrongContainer => "Wrong container",
        _ => "Other"
    };

    public static string ClosedMessage(SpecimenStatus status) =>
        $"This specimen has been {Describe(status)}, so it can't be changed.";

    /// <summary>A change must start from the version saved now, or someone else has changed it since (409).</summary>
    public static void EnsureVersion(Specimen specimen, int expectedVersion)
    {
        if (specimen.Version != expectedVersion)
            throw new ConflictException(ChangedMeanwhileMessage);
    }

    /// <summary>When the report is expected: taken plus the type's expected days.</summary>
    public static DateTime ExpectedBy(Specimen specimen) =>
        specimen.TakenAtUtc.AddDays(specimen.SpecimenType?.ExpectedTurnaroundDays ?? 0);

    /// <summary>Still open after it was expected back: a date comparison, nothing clinical.</summary>
    public static bool IsOverdueAtLab(Specimen specimen, DateTime nowUtc) =>
        IsOpen(specimen.Status) && ExpectedBy(specimen) < nowUtc;

    /// <summary>
    /// Why a specimen can't be recorded as taken at this stage now, or null if it can: the record can change, the
    /// pathway is open (not on hold or closed) and the stage can be edited.
    /// </summary>
    public static string? WhyCantTake(PatientPathway pathway, PatientPathwayStage stage)
    {
        if (PatientRules.IsAnonymised(pathway.Patient))
            return PatientRules.AnonymisedMessage;
        if (PatientRules.IsRestricted(pathway.Patient))
            return RestrictedTakeMessage;
        if (!PathwayWorkflow.IsOpen(pathway.Status))
            return $"A specimen can't be recorded while the pathway is {PathwayWorkflow.Describe(pathway.Status)}.";
        return PathwayWorkflow.WhyStageCantBeEdited(pathway, stage) is { } why
            ? $"A specimen can only be recorded at a stage being worked on. {why}"
            : null;
    }

    /// <summary>Why its details can't be corrected now (only before it's sent, the record not restricted), or null.</summary>
    public static string? WhyCantEdit(Specimen specimen) =>
        PatientRules.WhyCantChange(specimen.Patient)
        ?? (specimen.Status == Taken ? null : $"A specimen's details can only be changed before it's sent; this one has been {Describe(specimen.Status)}.");

    /// <summary>
    /// Why a lab step (to <paramref name="to"/>) can't be taken now, or null if it can: the status allows it and the
    /// record isn't anonymised (a restricted one's can go on: decision 75).
    /// </summary>
    public static string? WhyCantMoveTo(Specimen specimen, SpecimenStatus to)
    {
        if (PatientRules.IsAnonymised(specimen.Patient))
            return PatientRules.AnonymisedMessage;
        if (!IsOpen(specimen.Status))
            return ClosedMessage(specimen.Status);

        var allowed = to switch
        {
            Sent => specimen.Status == Taken,
            Received => specimen.Status == Sent,
            Reported => specimen.Status == Received,
            RejectedByLab => specimen.Status is Sent or Received,
            Lost or EnteredInError => true,
            _ => false
        };
        return allowed ? null : to switch
        {
            Sent => $"Only a specimen not yet sent can be sent; this one has been {Describe(specimen.Status)}.",
            Received => specimen.Status == Taken
                ? "Record the specimen as sent before the lab receives it."
                : $"This specimen has already been {Describe(specimen.Status)}.",
            Reported => "Record the specimen as received by the lab before attaching its report.",
            RejectedByLab => "Only a specimen sent to the lab can be rejected by it.",
            _ => $"A specimen can't be {Describe(to)} from {Describe(specimen.Status)}."
        };
    }

    /// <summary>
    /// Whether <paramref name="actor"/> may mark the specimen entered in error: a holder of CorrectRecords at any time,
    /// or whoever recorded it, while they still hold RecordSpecimens, within <see cref="OwnCorrectionWindow"/>.
    /// </summary>
    public static bool MayMarkEnteredInError(Specimen specimen, PathwayActor actor, DateTime nowUtc) =>
        actor.Has(Permission.CorrectRecords)
        || (actor.UserId == specimen.TakenByUserId
            && actor.Has(Permission.RecordSpecimens)
            && nowUtc < specimen.RecordedAtUtc + OwnCorrectionWindow);

    /// <summary>Whether <paramref name="actor"/> may mark it lost: holders of RecordSpecimens or ProcessSpecimens.</summary>
    public static bool MayMarkLost(PathwayActor actor) =>
        actor.Has(Permission.RecordSpecimens) || actor.Has(Permission.ProcessSpecimens);

    public const string EnteredInErrorNotAllowedMessage =
        "Only whoever recorded this specimen can mark it entered in error, within 24 hours of recording it. After that, ask an admin.";

    public const string LostNotAllowedMessage =
        "Your roles don't allow recording specimens, so you can't mark one lost.";

    /// <summary>
    /// Why a step's time can't be used, or null if it can: not in the future, at most <see cref="MaxBackdating"/> back,
    /// and not before <paramref name="previousUtc"/> (the step before), if given.
    /// </summary>
    public static string? WhyTimeInvalid(DateTime atUtc, DateTime? previousUtc, DateTime nowUtc, string step)
    {
        if (atUtc > nowUtc)
            return $"The time it was {step} can't be in the future.";
        if (atUtc < nowUtc - MaxBackdating)
            return $"The time it was {step} can be at most 7 days ago.";
        if (previousUtc is { } previous && atUtc < previous)
            return $"The time it was {step} can't be before the step before it.";
        return null;
    }
}
