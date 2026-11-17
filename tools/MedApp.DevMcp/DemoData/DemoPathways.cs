using MedApp.Domain.Procedures.PatientProcedures;
using Priority = MedApp.Domain.Procedures.PatientProcedures.PatientProcedurePriority;

namespace MedApp.DevMcp.DemoData;

/// <summary>Who works a stage: the procedure's lead clinician, or someone in a supporting role.</summary>
public enum StageRole
{
    /// <summary>The booking coordinator: referrals and scheduling.</summary>
    Coordinator,

    /// <summary>The procedure's lead clinician (its assignee): the surgeon, endoscopist, nurse or physiotherapist.</summary>
    Lead,

    /// <summary>The pre-assessment nurse.</summary>
    PreAssessment,

    /// <summary>Recovery on the day: a nurse or the healthcare assistant.</summary>
    Ward,

    /// <summary>Wound checks and follow-up calls: a nurse.</summary>
    FollowUp,
}

/// <summary>The kind of day a visit is best booked on, by the day template on that person's calendar.</summary>
public enum DayKind
{
    Any,
    Clinic,
    Theatre,
    Endoscopy,
}

/// <summary>A range of days, from which the seed picks at random.</summary>
public readonly record struct Days(double Min, double Max)
{
    public double Pick(Random random) => Min + random.NextDouble() * (Max - Min);
}

/// <summary>
/// How one stage of a pathway goes: who works it, how long it waits to be started (from the stage before it
/// finishing), how long the work takes, and its visit, if it involves one: how long, how soon after booking, and on
/// which kind of day. Waiting and working times differ by pathway, so Insights' stage times do too.
/// </summary>
public sealed record StageSpec(
    string Definition,
    StageRole Role,
    Days Wait,
    Days Work,
    int VisitMinutes = 0,
    int VisitMinutesMax = 0,
    Days Lead = default,
    DayKind Prefer = DayKind.Any,
    double VisitChance = 1);

/// <summary>A pathway: its template, its stages in order, due dates, priorities and the kinds of procedure it does.</summary>
public sealed record PathwaySpec(
    Pathway Pathway,
    string TemplateName,
    StageSpec[] Stages,
    int DueWeeks,
    int UrgentDueWeeks,
    (Priority Priority, int Weight)[] Priorities,
    bool IsArchived = false);

/// <summary>A stage definition the seed creates: what admins wrote, with its visit and SNOMED CT code.</summary>
public sealed record DefinitionSpec(
    string Name,
    string Description,
    string Instructions,
    VisitRequirement Visit,
    string? SnomedCode = null,
    string? SnomedTerm = null,
    bool IsArchived = false);

/// <summary>The clinic's stage definitions and pathways (templates).</summary>
public static class DemoPathways
{
    /// <summary>
    /// The stage definitions. SNOMED CT codes only where the concept is certain (each passes the format and Verhoeff
    /// check, <c>SnomedCt</c>); the rest are left blank, so both states show.
    /// </summary>
    public static readonly DefinitionSpec[] Definitions =
    [
        new("Referral received", "The referral has been received, logged and checked.",
            "Check the referral is complete and the patient's details match the GP record. Note the reason and any urgency.",
            VisitRequirement.None, "3457005", "Patient referral"),
        new("Triage", "A clinician has decided the route and the priority.",
            "Review the referral and any results. Decide clinic first or straight to test, and the priority.",
            VisitRequirement.None, "225390008", "Triage"),
        new("Consultation", "The patient has been seen by the clinician.",
            "Take the history, examine, discuss the options and record what was agreed.",
            VisitRequirement.Required, "11429006", "Consultation"),
        new("Pre-assessment", "Fitness for the procedure has been assessed.",
            "Observations, bloods and ECG as needed. Review medicines and allergies, and give the preparation instructions.",
            VisitRequirement.Required),
        new("Consent", "Informed consent has been obtained and recorded.",
            "Discuss benefits, risks and alternatives, including doing nothing. Record consent; give the patient a copy. Often done at a visit.",
            VisitRequirement.Suggested),
        new("Scheduling", "A date, time and place have been arranged.",
            "Agree the date with the patient, send the admission letter and the preparation instructions.",
            VisitRequirement.None),
        new("Procedure", "The procedure has been carried out.",
            "Complete the safety checklist. Record what was done, what was found and any specimens sent.",
            VisitRequirement.Required),
        new("Recovery", "Recovery on the day is complete and the patient has gone home.",
            "Observations until stable, pain controlled, eating and drinking. Confirm an escort and give the discharge advice.",
            VisitRequirement.None),
        new("Wound check", "The wound has been checked.",
            "Check the wound, remove sutures or clips if due, and look for signs of infection.",
            VisitRequirement.Required),
        new("Post-procedure review", "The surgeon has reviewed the recovery.",
            "Review progress, any results and the wound. Decide discharge or further follow-up.",
            VisitRequirement.Required),
        new("Results review", "Results or histology have been reviewed and explained.",
            "Review the reports, act on anything abnormal and explain the results to the patient. Can be by phone.",
            VisitRequirement.Suggested),
        new("Follow-up", "Follow-up is done, or the patient is discharged.",
            "Check recovery, then discharge to the GP or arrange more follow-up.",
            VisitRequirement.Suggested, "390906007", "Follow-up encounter"),
        new("Physiotherapy session", "A physiotherapy session has taken place.",
            "Assess, treat and progress the home exercise programme. Record measurements against the goals.",
            VisitRequirement.Required, "91251008", "Physical therapy procedure"),
        new("Discharge", "The patient has been discharged from the service.",
            "Confirm the goals are met, give self-management advice and send the discharge letter to the GP.",
            VisitRequirement.None, "58000006", "Patient discharge"),
        // Retired: consent is recorded with the Consent stage now. Archived, so admins see it but can't add it to templates.
        new("Paper consent (retired)", "Consent recorded on the old paper form.",
            "No longer used: record consent with the Consent stage.",
            VisitRequirement.None, IsArchived: true),
    ];

    private static readonly (Priority, int)[] RoutinePriorities = [(Priority.Low, 20), (Priority.Medium, 55), (Priority.High, 20), (Priority.Urgent, 5)];

    /// <summary>The pathways, each a template.</summary>
    public static readonly PathwaySpec[] Pathways =
    [
        new(Pathway.DayCase, "Day-case pathway",
        [
            new("Referral received", StageRole.Coordinator, new(0, 1), new(0.05, 0.5)),
            new("Pre-assessment", StageRole.PreAssessment, new(1, 4), default, 45, 45, new(2, 7), DayKind.Clinic),
            new("Consent", StageRole.Lead, new(0.5, 3), new(0.2, 2), 30, 30, new(2, 6), DayKind.Clinic, VisitChance: 0.6),
            new("Scheduling", StageRole.Coordinator, new(0, 2), new(0.5, 3)),
            new("Procedure", StageRole.Lead, new(0.5, 2), default, 60, 90, new(4, 12), DayKind.Theatre),
            new("Recovery", StageRole.Ward, new(0.005, 0.02), new(0.08, 0.2)),
            new("Follow-up", StageRole.FollowUp, new(5, 10), new(0.1, 1), 20, 20, new(2, 6), DayKind.Clinic, VisitChance: 0.5),
        ], DueWeeks: 6, UrgentDueWeeks: 4, RoutinePriorities),

        new(Pathway.DayCase2024, "Day-case pathway (2024)",
        [
            new("Referral received", StageRole.Coordinator, new(0, 2), new(0.1, 1)),
            new("Pre-assessment", StageRole.PreAssessment, new(2, 6), default, 45, 45, new(2, 6), DayKind.Clinic),
            new("Consent", StageRole.Lead, new(1, 3), new(0.5, 2), 30, 30, new(2, 5), DayKind.Clinic, VisitChance: 0.5),
            new("Procedure", StageRole.Lead, new(2, 6), default, 60, 90, new(3, 8), DayKind.Theatre),
            new("Follow-up", StageRole.FollowUp, new(4, 8), new(0.2, 1), 20, 20, new(1, 4), DayKind.Clinic, VisitChance: 0.5),
        ], DueWeeks: 6, UrgentDueWeeks: 4, RoutinePriorities, IsArchived: true),

        new(Pathway.Surgical, "Standard surgical pathway",
        [
            new("Consultation", StageRole.Lead, new(2, 8), default, 30, 30, new(3, 10), DayKind.Clinic),
            new("Pre-assessment", StageRole.PreAssessment, new(12, 32), default, 45, 45, new(3, 10), DayKind.Clinic),
            new("Consultation", StageRole.Lead, new(3, 10), default, 30, 30, new(4, 12), DayKind.Clinic),
            new("Consent", StageRole.Lead, new(1, 5), new(0.5, 3), 30, 30, new(2, 7), DayKind.Clinic, VisitChance: 0.7),
            new("Scheduling", StageRole.Coordinator, new(14, 40), new(1, 5)),
            new("Procedure", StageRole.Lead, new(1, 3), default, 90, 90, new(7, 18), DayKind.Theatre),
            new("Recovery", StageRole.Ward, new(0.005, 0.02), new(0.12, 0.3)),
            new("Wound check", StageRole.FollowUp, new(5, 9), default, 15, 15, new(1, 4), DayKind.Clinic),
            new("Post-procedure review", StageRole.Lead, new(20, 34), default, 20, 20, new(3, 10), DayKind.Clinic),
        ], DueWeeks: 18, UrgentDueWeeks: 8, [(Priority.Low, 15), (Priority.Medium, 50), (Priority.High, 30), (Priority.Urgent, 5)]),

        new(Pathway.Diagnostic, "Diagnostic procedure",
        [
            new("Referral received", StageRole.Coordinator, new(0, 0.5), new(0.05, 0.3)),
            new("Triage", StageRole.Lead, new(0.2, 2), new(0.05, 0.5)),
            new("Consultation", StageRole.Lead, new(1, 5), default, 30, 30, new(3, 9), DayKind.Clinic),
            new("Consent", StageRole.Lead, new(0, 1), new(0.1, 1), 30, 30, new(1, 4), DayKind.Clinic, VisitChance: 0.4),
            new("Scheduling", StageRole.Coordinator, new(7, 20), new(0.5, 2)),
            new("Procedure", StageRole.Lead, new(0.5, 2), default, 60, 75, new(3, 10), DayKind.Endoscopy),
            new("Results review", StageRole.Lead, new(4, 10), new(0.2, 2), 20, 20, new(2, 6), DayKind.Clinic, VisitChance: 0.35),
        ], DueWeeks: 6, UrgentDueWeeks: 2, [(Priority.Low, 5), (Priority.Medium, 45), (Priority.High, 25), (Priority.Urgent, 25)]),

        new(Pathway.Minor, "Minor procedure (clinic)",
        [
            new("Consultation", StageRole.Lead, new(2, 8), default, 30, 30, new(3, 9), DayKind.Clinic),
            new("Consent", StageRole.Lead, new(0, 0.2), new(0.02, 0.2), 0, 0, default, DayKind.Clinic, VisitChance: 0),
            new("Procedure", StageRole.Lead, new(1, 4), default, 60, 60, new(5, 14), DayKind.Clinic),
            new("Follow-up", StageRole.Lead, new(7, 11), new(0.1, 0.5), 20, 20, new(1, 4), DayKind.Clinic, VisitChance: 0.6),
        ], DueWeeks: 8, UrgentDueWeeks: 4, [(Priority.Low, 50), (Priority.Medium, 45), (Priority.High, 5)]),

        new(Pathway.Physio, "Physiotherapy course",
        [
            new("Consultation", StageRole.Lead, new(1, 5), default, 30, 30, new(2, 7), DayKind.Clinic),
            new("Physiotherapy session", StageRole.Lead, new(0.5, 2), default, 45, 45, new(4, 7), DayKind.Clinic),
            new("Physiotherapy session", StageRole.Lead, new(0.2, 1), default, 45, 45, new(5, 7), DayKind.Clinic),
            new("Physiotherapy session", StageRole.Lead, new(0.2, 1), default, 45, 45, new(5, 7), DayKind.Clinic),
            new("Physiotherapy session", StageRole.Lead, new(0.2, 1), default, 45, 45, new(5, 7), DayKind.Clinic),
            new("Discharge", StageRole.Lead, new(0.2, 2), new(0.1, 1)),
        ], DueWeeks: 10, UrgentDueWeeks: 6, [(Priority.Low, 40), (Priority.Medium, 55), (Priority.High, 5)]),
    ];

    public static PathwaySpec For(Pathway pathway) => Pathways.Single(p => p.Pathway == pathway);
}
