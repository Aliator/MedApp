using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Domain.Results;
using static MedApp.Domain.Results.ResultStatus;

namespace MedApp.Application.Results.Common;

/// <summary>
/// The rules of a result's sign-off (<c>docs/centre-phases-3-5-plan.md</c>, decisions 91 to 97). Handlers check here
/// before changing a result, and the response's <c>Can...</c> flags come from the same rules.
/// <list type="bullet">
/// <item>Awaiting sign-off, then optionally Seen, then SignedOff (with what was done, chosen by the clinician), which is
/// final. Any holder of SignOffResults may mark it seen, sign it off, take it over or reassign it.</item>
/// <item>Entered in error (decision 96): by a holder of CorrectRecords at any time, or whoever reported it within 24
/// hours, while they still hold the permission that let them (ProcessSpecimens for a specimen's, WorkPathways for an
/// external one). It closes the result, deletes its file, and sends a specimen back to Received to be reported again.</item>
/// <item>A restricted record (decision 75): a result can still be seen, signed off, reassigned and entered in error (a
/// result must never be stuck); a new external one can't be recorded. An anonymised record's can't change at all.</item>
/// </list>
/// <b>MedApp never reads, interprets or flags a result's contents</b>: the actions are the clinician's own record, and
/// MedApp never checks that one fits.
/// </summary>
public static class ResultWorkflow
{
    /// <summary>How long whoever reported a result may mark it entered in error themselves (CorrectRecords any time).</summary>
    public static readonly TimeSpan OwnCorrectionWindow = TimeSpan.FromHours(24);

    /// <summary>The statuses that still await sign-off.</summary>
    public static readonly IReadOnlyList<ResultStatus> AwaitingStatuses = [AwaitingSignOff, Seen];

    /// <summary>The actions in the order every picker lists them (none pre-selected).</summary>
    public static readonly IReadOnlyList<ResultAction> ActionsInOrder =
    [
        ResultAction.NoFurtherAction, ResultAction.PatientInformed, ResultAction.AppointmentArranged,
        ResultAction.ForMdtDiscussion, ResultAction.ReferredOn, ResultAction.Other,
    ];

    public const string ChangedMeanwhileMessage =
        "Someone else changed this result while you had it open. Reload it to see their change, then try again.";

    public const string RestrictedRecordMessage =
        "This patient's record is restricted, so a result can't be recorded for them until an admin lifts the restriction. " +
        "Ask an admin what to do with the report.";

    public const string ReportFileMessage = "This file is a result's report, so it can't be deleted on its own.";

    public const string EnteredInErrorNotAllowedMessage =
        "Only whoever reported this result can mark it entered in error, within 24 hours of reporting it. After that, ask someone who can correct records.";

    public const string ResponsibleNotClinicianMessage =
        "Choose a clinician whose roles allow signing off results.";

    public const string SameResponsibleMessage = "They're already responsible for this result.";

    public static bool IsAwaiting(ResultStatus status) => AwaitingStatuses.Contains(status);

    /// <summary>A status in words, for messages: "awaiting sign-off", "signed off".</summary>
    public static string Describe(ResultStatus status) => status switch
    {
        AwaitingSignOff => "awaiting sign-off",
        Seen => "seen",
        SignedOff => "signed off",
        EnteredInError => "entered in error",
        _ => status.ToString()
    };

    /// <summary>An action in words: "To be discussed at MDT".</summary>
    public static string Describe(ResultAction action) => action switch
    {
        ResultAction.NoFurtherAction => "No further action",
        ResultAction.PatientInformed => "Patient informed",
        ResultAction.AppointmentArranged => "Appointment arranged",
        ResultAction.ForMdtDiscussion => "To be discussed at MDT",
        ResultAction.ReferredOn => "Referred on",
        _ => "Other"
    };

    /// <summary>A change must start from the version saved now, or someone else has changed it since (409).</summary>
    public static void EnsureVersion(Result result, int expectedVersion)
    {
        if (result.Version != expectedVersion)
            throw new ConflictException(ChangedMeanwhileMessage);
    }

    /// <summary>
    /// Why the result can't move on now (seen, signed off, reassigned), or null if it can: it awaits sign-off and the
    /// record isn't anonymised (a restricted one's can: decision 75).
    /// </summary>
    public static string? WhyCantProgress(Result result)
    {
        if (PatientRules.IsAnonymised(result.Patient))
            return PatientRules.AnonymisedMessage;
        return IsAwaiting(result.Status)
            ? null
            : $"This result has been {Describe(result.Status)}, so it can't be changed. Anything later goes in a pathway note.";
    }

    /// <summary>Why it can't be marked seen now (only while awaiting, not already seen), or null.</summary>
    public static string? WhyCantMarkSeen(Result result) =>
        WhyCantProgress(result) ?? (result.Status == Seen ? "This result has already been marked seen." : null);

    /// <summary>Why it can't be marked entered in error now, or null: not already, and not anonymised.</summary>
    public static string? WhyCantMarkEnteredInError(Result result)
    {
        if (PatientRules.IsAnonymised(result.Patient))
            return PatientRules.AnonymisedMessage;
        return result.Status == EnteredInError ? "This result has already been marked entered in error." : null;
    }

    /// <summary>
    /// Whether <paramref name="actor"/> may mark it entered in error: a holder of CorrectRecords at any time, or whoever
    /// reported it, while they still hold the permission that let them, within <see cref="OwnCorrectionWindow"/>.
    /// </summary>
    public static bool MayMarkEnteredInError(Result result, PathwayActor actor, DateTime nowUtc) =>
        actor.Has(Permission.CorrectRecords)
        || (actor.UserId == result.ReportedByUserId
            && actor.Has(result.Source == ResultSource.Specimen ? Permission.ProcessSpecimens : Permission.WorkPathways)
            && nowUtc < result.ReportedAtUtc + OwnCorrectionWindow);

    /// <summary>Whole days since it was reported, for the lists: date arithmetic, nothing clinical.</summary>
    public static int AgeDays(Result result, DateTime nowUtc) =>
        Math.Max(0, (int)(nowUtc - result.ReportedAtUtc).TotalDays);

    /// <summary>
    /// Why a stage can't be finished for want of its results' sign-off, or null if it can: a stage that waits for
    /// results, or the pathway's last stage, can't be finished while any of <paramref name="awaiting"/> (the pathway's
    /// results awaiting sign-off) is still awaiting (decision 95). Dates only, never the results' contents.
    /// </summary>
    public static string? WhyAwaitingResultsBlockFinish(IReadOnlyCollection<Result> awaiting, bool isLastStage, bool waitsForResults)
    {
        if (!isLastStage && !waitsForResults)
            return null;
        var open = awaiting.Where(r => IsAwaiting(r.Status)).OrderBy(r => r.ReportedAtUtc).ThenBy(r => r.Id).ToList();
        if (open.Count == 0)
            return null;

        var dates = string.Join(", ", open.Select(r => UkDate(r.ReportedAtUtc)));
        var count = open.Count == 1 ? "a result awaits" : $"{open.Count} results await";
        var which = isLastStage ? "The pathway's last stage" : "This stage waits for results: it";
        return $"{which} can't be finished while {count} sign-off (reported {dates}). " +
               "Sign each one off on the Results page, or mark it entered in error.";
    }

    private static string UkDate(DateTime utc) =>
        Calendar.Common.UkTime.DateOf(utc).ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
}
