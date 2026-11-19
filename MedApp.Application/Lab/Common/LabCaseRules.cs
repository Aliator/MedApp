using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Domain.Lab;

namespace MedApp.Application.Lab.Common;

/// <summary>What a case needs next: its sample, a step, a choice's outcome, or its results (the light demo's <c>NextWork</c>).</summary>
public sealed record LabNext(
    LabNextKind Kind,
    string Name,
    LabTestStep? TestStep = null,
    LabChoice? Choice = null,
    IReadOnlyList<LabOutcome>? Outcomes = null);

public enum LabNextKind
{
    Receive,
    Step,
    Choice,
    Report
}

/// <summary>
/// The rules of a lab case's work (MC15 of <c>docs/multi-clinic-plan.md</c>; the light demo's <c>labCases.ts</c>):
/// Received, Accessioned, the choice "Sample adequate?", the test's required steps in order, then the choice "Needs more
/// work?" (each extra outcome adds the test's optional steps of its kind, then asks again), then the results. The lab
/// member records each outcome; MedApp never chooses or suggests one (decision 191).
/// </summary>
public static class LabCaseRules
{
    public const string ChangedMeanwhileMessage =
        "Someone else changed this lab case while you had it open. Reload it to see their change, then try again.";

    public const string LabWorkMessage = "Lab cases are worked by the lab team.";

    public const string AnonymisedMessage = "This patient's record has been anonymised, so the case can't be changed.";

    public const string AwaitingSurgeryDetailsMessage =
        "The report has gone to the hospital, so the steps and report can’t be changed. The hospital’s surgery details can still be added.";

    public const string SurgeryDetailsHospitalOnlyMessage = "Surgery details come only with a hospital’s case.";

    public const string SurgeryDetailsClosedMessage = "This case is closed, so no more surgery details can be added.";

    public const string NoSurgeryFieldsMessage =
        "With no surgery expected, leave the surgery’s date, procedure and outcome empty.";

    public const string NoSurgeryWhyMessage = "Say why no surgery is expected, as the hospital wrote it.";

    public const string NoSurgeryNotSentMessage =
        "“No surgery expected” closes the case: record it once the report has been sent.";

    public const string SurgeryDetailsMissingMessage =
        "Enter what the hospital sent: the date, the procedure, the outcome or other details.";

    /// <summary>The letter a hospital case's report goes back as (MC20's <c>LetterKind.LabReport = 6</c>, its PDF to come).</summary>
    public const string LabReportLetterKind = "LabReport";

    /// <summary>Whom a report is addressed to when its request isn't read with it.</summary>
    public const string UnknownSurgeon = "The requesting surgeon";

    public const string LatestStepOnlyMessage = "Only the latest step recorded can be marked entered in error.";

    public const string OwnStepOnlyMessage = "Only whoever recorded a step can mark it entered in error.";

    public const string CaseEnteredInErrorNotAllowedMessage =
        "Only whoever opened this case can mark it entered in error, within 24 hours of opening it. After that, ask someone who can correct records.";

    public const string WhatWasWrongMessage = "Say what was wrong.";

    public const string InsufficientDetailMessage =
        "Say why the sample is insufficient: the clinic or hospital will need to know.";

    public const string SecondOpinionWhereMessage = "Say where it went for the second opinion.";

    public const string ResultsMissingMessage = "Enter the results.";

    public static readonly string ResultsTooLongMessage =
        $"The results must be at most {LabCase.MaxReportTextLength:N0} characters.";

    public const string ReceiveName = "Receive the sample";
    public const string AccessionedName = "Accessioned";
    public const string ReportName = "Results and report";

    /// <summary>How long whoever opened a case may mark it entered in error themselves (CorrectRecords any time).</summary>
    public static readonly TimeSpan OwnCorrectionWindow = TimeSpan.FromHours(24);

    /// <summary>The questions the choices ask, and their outcomes in the fixed order they're offered.</summary>
    public static string Question(LabChoice choice) => choice switch
    {
        LabChoice.SampleAdequate => "Sample adequate?",
        _ => "Needs more work?"
    };

    public static IReadOnlyList<LabOutcome> OutcomesOf(LabChoice choice) => choice switch
    {
        LabChoice.SampleAdequate => [LabOutcome.Adequate, LabOutcome.Insufficient],
        _ => [LabOutcome.NoMoreWork, LabOutcome.ExtraTests, LabOutcome.SecondOpinion]
    };

    /// <summary>An outcome in words, as the page shows it.</summary>
    public static string Describe(LabOutcome outcome) => outcome switch
    {
        LabOutcome.Adequate => "Adequate",
        LabOutcome.Insufficient => "Insufficient: ask for a repeat",
        LabOutcome.NoMoreWork => "No more work",
        LabOutcome.ExtraTests => "Extra tests (special stains)",
        _ => "A second opinion"
    };

    /// <summary>A status in words, for messages: "reported", "closed with a repeat sample requested".</summary>
    public static string Describe(LabCaseStatus status) => status switch
    {
        LabCaseStatus.Reported => "reported",
        LabCaseStatus.RepeatRequested => "closed with a repeat sample requested",
        LabCaseStatus.EnteredInError => "marked entered in error",
        LabCaseStatus.Closed => "closed",
        LabCaseStatus.AwaitingSurgeryDetails => "reported and sent to the hospital",
        _ => status.ToString()
    };

    public static string ClosedMessage(LabCaseStatus status) => $"This case has been {Describe(status)}, so it can't be changed.";

    /// <summary>The steps still standing, in the order recorded.</summary>
    public static IReadOnlyList<LabCaseStep> LiveSteps(LabCase labCase) =>
        labCase.Steps.Where(s => !s.IsEnteredInError).OrderBy(s => s.Sequence).ToList();

    /// <summary>The latest step still standing: the only one that can be marked entered in error (not Received).</summary>
    public static LabCaseStep? LatestLive(LabCase labCase) => LiveSteps(labCase).LastOrDefault();

    public static bool IsReceived(LabCase labCase) => LiveSteps(labCase).Any(s => s.Kind == LabCaseStepKind.Received);

    /// <summary>The test's optional steps an extra outcome adds: its extra tests, or the second opinion's two.</summary>
    public static IReadOnlyList<LabTestStep> ExtraSteps(LabTest? test, LabOutcome? outcome)
    {
        LabStepKind[] kinds = outcome switch
        {
            LabOutcome.ExtraTests => [LabStepKind.ExtraTests],
            LabOutcome.SecondOpinion => [LabStepKind.SentForSecondOpinion, LabStepKind.SecondOpinionReturned],
            _ => []
        };
        return (test?.Steps ?? []).OrderBy(s => s.Position).Where(s => s.IsOptional && kinds.Contains(s.Kind)).ToList();
    }

    /// <summary>A choice, with the outcomes its test allows (more work only when the test has steps for it).</summary>
    private static LabNext ChoiceOf(LabChoice choice, LabTest? test) =>
        new(LabNextKind.Choice, Question(choice), Choice: choice, Outcomes: OutcomesOf(choice)
            .Where(o => o is not (LabOutcome.ExtraTests or LabOutcome.SecondOpinion) || ExtraSteps(test, o).Count > 0)
            .ToList());

    /// <summary>What an open case needs next, or null once it's closed.</summary>
    public static LabNext? Next(LabCase labCase)
    {
        if (labCase.Status != LabCaseStatus.Open)
            return null;

        var steps = LiveSteps(labCase);
        var test = labCase.LabTest;
        if (!steps.Any(s => s.Kind == LabCaseStepKind.Received))
            return new LabNext(LabNextKind.Receive, ReceiveName);
        if (!steps.Any(s => s.Kind == LabCaseStepKind.Accessioned))
            return new LabNext(LabNextKind.Step, AccessionedName);
        if (!steps.Any(s => s.Choice == LabChoice.SampleAdequate))
            return ChoiceOf(LabChoice.SampleAdequate, test);

        foreach (var required in (test?.Steps ?? []).OrderBy(s => s.Position).Where(s => !s.IsOptional))
            if (!steps.Any(s => s.TestStepId == required.Id))
                return new LabNext(LabNextKind.Step, required.Name, required);

        var at = -1;
        for (var i = steps.Count - 1; i >= 0; i--)
            if (steps[i].Choice == LabChoice.NeedsMoreWork)
            {
                at = i;
                break;
            }

        if (at < 0)
            return ChoiceOf(LabChoice.NeedsMoreWork, test);

        var last = steps[at];
        if (last.Outcome == LabOutcome.NoMoreWork)
            return new LabNext(LabNextKind.Report, ReportName);

        var after = steps.Skip(at + 1).ToList();
        foreach (var extra in ExtraSteps(test, last.Outcome))
            if (!after.Any(s => s.TestStepId == extra.Id))
                return new LabNext(LabNextKind.Step, extra.Name, extra);

        return ChoiceOf(LabChoice.NeedsMoreWork, test);
    }

    /// <summary>Why the next thing asked for isn't <paramref name="next"/> (the 409's words).</summary>
    public static string NotNext(LabNext? next) => next?.Kind switch
    {
        LabNextKind.Choice => $"Record the outcome of “{next.Name}” first.",
        LabNextKind.Report => "Every step is done: enter the results and authorise the report.",
        _ => $"The next step is {next?.Name ?? "none"}."
    };

    /// <summary>A step's name: the fixed ones, a choice's question, or its test step's name.</summary>
    public static string StepName(LabCase labCase, LabCaseStep step) => step.Kind switch
    {
        LabCaseStepKind.Received => "Received",
        LabCaseStepKind.Accessioned => AccessionedName,
        LabCaseStepKind.Outcome => step.Choice is { } choice ? Question(choice) : "Outcome",
        _ => labCase.LabTest?.Steps.FirstOrDefault(t => t.Id == step.TestStepId)?.Name ?? "Step"
    };

    /// <summary>Whether the actor works lab cases: the lab team, with ProcessSpecimens (admins read only).</summary>
    public static bool WorksCases(PathwayActor actor, Guid? labTeamId) =>
        actor.Has(Permission.ProcessSpecimens) && LabWalls.IsLabMember(actor.Teams, labTeamId) && !actor.Teams.SeesAllTeams;

    /// <summary>Why the viewer can't work the case now, or null if they can.</summary>
    public static string? ReadOnlyReason(LabCase labCase, PathwayActor viewer, Guid? labTeamId)
    {
        if (labCase.Status == LabCaseStatus.AwaitingSurgeryDetails)
            return AwaitingSurgeryDetailsMessage;
        if (labCase.Status != LabCaseStatus.Open)
            return ClosedMessage(labCase.Status);
        if (PatientRules.IsAnonymised(labCase.Patient))
            return AnonymisedMessage;
        if (!WorksCases(viewer, labTeamId))
            return LabWorkMessage;
        return null;
    }

    /// <summary>
    /// Whether the viewer may mark the case entered in error: an open case, the record not anonymised, and a holder of
    /// CorrectRecords, or whoever opened it, working cases, within 24 hours.
    /// </summary>
    public static bool MayMarkEnteredInError(LabCase labCase, PathwayActor viewer, Guid? labTeamId, DateTime nowUtc) =>
        labCase.Status == LabCaseStatus.Open
        && !PatientRules.IsAnonymised(labCase.Patient)
        && (viewer.Has(Permission.CorrectRecords)
            || (labCase.OpenedByUserId == viewer.UserId && WorksCases(viewer, labTeamId) && nowUtc < labCase.OpenedAtUtc + OwnCorrectionWindow));

    /// <summary>A change must start from the version saved now (409).</summary>
    public static void EnsureVersion(LabCase labCase, int expectedVersion)
    {
        if (labCase.Version != expectedVersion)
            throw new ConflictException(ChangedMeanwhileMessage);
    }

    /// <summary>
    /// Ready to work on (the handler checks first that the actor works cases): the record isn't anonymised, the version
    /// is the one shown and the case is open (409s).
    /// </summary>
    public static void EnsureCanWork(LabCase labCase, int expectedVersion)
    {
        if (PatientRules.IsAnonymised(labCase.Patient))
            throw new ConflictException(AnonymisedMessage);
        EnsureVersion(labCase, expectedVersion);
        if (labCase.Status != LabCaseStatus.Open)
            throw new ConflictException(ClosedMessage(labCase.Status));
    }

    /// <summary>Whether surgery details can still arrive (MC20): a hospital case, open or awaiting them.</summary>
    public static bool TakesSurgeryDetails(LabCase labCase) =>
        labCase.Source == MedApp.Domain.Specimens.SpecimenSource.Hospital
        && labCase.Status is LabCaseStatus.Open or LabCaseStatus.AwaitingSurgeryDetails;

    /// <summary>Whether the viewer may add surgery details now: they work cases, the case takes them, and the record isn't anonymised.</summary>
    public static bool MayAddSurgeryDetails(LabCase labCase, PathwayActor viewer, Guid? labTeamId) =>
        WorksCases(viewer, labTeamId) && TakesSurgeryDetails(labCase) && !PatientRules.IsAnonymised(labCase.Patient);

    /// <summary>Free text: trimmed, or null when empty.</summary>
    public static string? Optional(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
