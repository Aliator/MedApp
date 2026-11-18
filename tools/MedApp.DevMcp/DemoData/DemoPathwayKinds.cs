using MedApp.Domain.Pathways.PatientPathways;
using Priority = MedApp.Domain.Pathways.PatientPathways.PatientPathwayPriority;

namespace MedApp.DevMcp.DemoData;

/// <summary>Who works a stage: the pathway's lead clinician, or someone in a supporting role.</summary>
public enum StageRole
{
    /// <summary>The booking coordinator: referrals as they arrive.</summary>
    Coordinator,

    /// <summary>The pathway's lead clinician (its assignee): the consultant, GP, nurse specialist or podiatrist.</summary>
    Lead,

    /// <summary>The lab: the biomedical scientist, for histology and for blood and urine tests.</summary>
    Lab,

    /// <summary>Whoever takes the bloods and collects the sample: the healthcare assistant, or a nurse specialist.</summary>
    Samples,

    /// <summary>Follow-up by a clinical nurse specialist.</summary>
    Nurse,

    /// <summary>The pharmacist: medicines reviews.</summary>
    Pharmacist,
}

/// <summary>The kind of day a visit is best booked on, by the day template on that person's calendar.</summary>
public enum DayKind
{
    Any,
    Clinic,
    MinorSurgery,
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

/// <summary>A pathway: its template, its stages in order, due dates, priorities and the kinds of referral it takes.</summary>
public sealed record PathwayKindSpec(
    PathwayKind PathwayKind,
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

/// <summary>The centre's stage definitions and pathways (pathway templates).</summary>
public static class DemoPathwayKinds
{
    public const string ReferralReceived = "Referral received";
    public const string Triage = "Triage";
    public const string FirstClinicVisit = "First clinic visit";
    public const string BiopsyOrSample = "Biopsy or sample";
    public const string Histology = "Histology (lab)";
    public const string ResultsAndTeamReview = "Results and team review";
    public const string Treatment = "Treatment";
    public const string FollowUp = "Follow-up";
    public const string DischargeLetter = "Discharge letter to GP";
    public const string BloodAndUrineTests = "Blood and urine tests";
    public const string LabTests = "Lab tests (lab)";
    public const string ClinicReview = "Clinic review";
    public const string MedicinesReview = "Medicines review";
    public const string BiomechanicsSession = "Biomechanics session";

    /// <summary>
    /// The stage definitions. Visits are Required where the patient must be seen (clinic visits, biopsies, samples,
    /// treatment, sessions), Suggested where a phone call often does (results, medicines, follow-up), and none for the
    /// lab's work and for letters. SNOMED CT codes only where the concept is certain (each passes the format and
    /// Verhoeff check, <c>SnomedCt</c>); the rest are left blank, so both states show.
    /// </summary>
    public static readonly DefinitionSpec[] Definitions =
    [
        new(ReferralReceived, "The referral has been received, logged and checked.",
            "Check the referral is complete and the patient's details match the GP record. Note the reason and any urgency the referrer gave.",
            VisitRequirement.None, "3457005", "Patient referral"),
        new(Triage, "A clinician has read the referral and decided the route and the priority.",
            "Read the referral and anything sent with it (photographs, results). Decide which clinic, and the priority.",
            VisitRequirement.None, "225390008", "Triage"),
        new(FirstClinicVisit, "The patient has been seen in clinic for the first time.",
            "Take the history, examine, explain what happens next and record what was agreed.",
            VisitRequirement.Required, "11429006", "Consultation"),
        new(BiopsyOrSample, "A biopsy or sample has been taken and sent to the lab.",
            "Confirm the patient, the site and consent. Label the specimen at the bedside and complete the lab request.",
            VisitRequirement.Required, "86273004", "Biopsy"),
        new(Histology, "The lab has processed and reported the specimen.",
            "Book the specimen in, check its label against the request, process and report. Authorise the report.",
            VisitRequirement.None),
        new(ResultsAndTeamReview, "The results have been reviewed with the team and given to the patient.",
            "Review the report, take it to the team meeting if needed, agree the plan and tell the patient. Can be by phone.",
            VisitRequirement.Suggested),
        new(Treatment, "The agreed treatment has been given.",
            "Confirm the patient, the site and consent. Record what was done, any specimen sent and the aftercare advice.",
            VisitRequirement.Required),
        new(FollowUp, "Follow-up is done.",
            "Check how the patient is, answer questions, and decide whether anything more is needed. Can be by phone.",
            VisitRequirement.Suggested, "390906007", "Follow-up encounter"),
        new(DischargeLetter, "The discharge letter has gone to the GP, with a copy to the patient.",
            "Summarise what was found, what was done and what the GP is asked to do. Copy to the patient.",
            VisitRequirement.None, "58000006", "Patient discharge"),
        new(BloodAndUrineTests, "Bloods have been taken and a urine sample collected.",
            "Confirm the patient, take the bloods on the request and collect the urine sample. Label at the bedside; send to the lab the same day.",
            VisitRequirement.Required),
        new(LabTests, "The lab has run and reported the blood and urine tests.",
            "Book the samples in, run the tests on the request and authorise the results.",
            VisitRequirement.None, "15220000", "Laboratory test"),
        new(ClinicReview, "The patient has been reviewed in clinic with their results.",
            "Review the results with the patient, examine as needed, agree targets and record the plan.",
            VisitRequirement.Required),
        new(MedicinesReview, "The pharmacist has reviewed the patient's medicines.",
            "Check what is taken and how, side effects and interactions. Agree any changes and tell the GP. Can be by phone.",
            VisitRequirement.Suggested, "182836005", "Review of medication"),
        new(BiomechanicsSession, "A biomechanics session has taken place.",
            "Assess, treat and progress the home programme. Record measurements against the goals.",
            VisitRequirement.Required),
        // Retired: triage is recorded with the Triage stage now. Archived, so admins see it but can't add it to templates.
        new("Paper triage form (retired)", "Triage recorded on the old paper form.",
            "No longer used: record the outcome with the Triage stage.",
            VisitRequirement.None, IsArchived: true),
    ];

    private static readonly (Priority, int)[] RoutinePriorities = [(Priority.Low, 30), (Priority.Medium, 55), (Priority.High, 13), (Priority.Urgent, 2)];
    private static readonly (Priority, int)[] SkinLesionPriorities = [(Priority.Low, 10), (Priority.Medium, 45), (Priority.High, 25), (Priority.Urgent, 20)];

    /// <summary>
    /// The pathways, each a pathway template. The slow parts are where a centre's are: booking the first clinic visit
    /// (longest for the visiting consultants) and waiting for histology.
    /// </summary>
    public static readonly PathwayKindSpec[] PathwayKinds =
    [
        new(PathwayKind.SkinLesion, "Skin lesion pathway",
        [
            new(ReferralReceived, StageRole.Coordinator, new(0, 1), new(0.05, 0.5)),
            new(Triage, StageRole.Lead, new(0.2, 2), new(0.05, 0.5)),
            new(FirstClinicVisit, StageRole.Lead, new(1, 4), default, 30, 30, new(4, 11), DayKind.Clinic),
            new(BiopsyOrSample, StageRole.Lead, new(0.5, 3), default, 30, 45, new(2, 7), DayKind.MinorSurgery),
            new(Histology, StageRole.Lab, new(0.3, 1.5), new(5, 11)),
            new(ResultsAndTeamReview, StageRole.Lead, new(1, 4), new(0.2, 2), 20, 20, new(2, 6), DayKind.Clinic, VisitChance: 0.45),
            new(Treatment, StageRole.Lead, new(1, 4), default, 45, 60, new(4, 9), DayKind.MinorSurgery),
            new(FollowUp, StageRole.Nurse, new(5, 9), new(0.1, 1), 20, 20, new(2, 6), DayKind.Clinic, VisitChance: 0.5),
            new(DischargeLetter, StageRole.Lead, new(0.2, 2), new(0.1, 1)),
        ], DueWeeks: 12, UrgentDueWeeks: 6, SkinLesionPriorities),

        new(PathwayKind.SkinLesion2024, "Skin lesion pathway (2024)",
        [
            new(ReferralReceived, StageRole.Coordinator, new(0, 2), new(0.1, 1)),
            new(FirstClinicVisit, StageRole.Lead, new(2, 6), default, 30, 30, new(3, 10), DayKind.Clinic),
            new(BiopsyOrSample, StageRole.Lead, new(1, 4), default, 30, 45, new(3, 8), DayKind.MinorSurgery),
            new(Histology, StageRole.Lab, new(0.5, 2), new(6, 14)),
            new(FollowUp, StageRole.Lead, new(4, 8), new(0.2, 1), 20, 20, new(1, 4), DayKind.Clinic, VisitChance: 0.5),
        ], DueWeeks: 8, UrgentDueWeeks: 4, SkinLesionPriorities, IsArchived: true),

        new(PathwayKind.InflammatorySkin, "Inflammatory skin review",
        [
            new(ReferralReceived, StageRole.Coordinator, new(0, 1), new(0.05, 0.5)),
            new(Triage, StageRole.Lead, new(0.2, 2), new(0.05, 0.5)),
            new(FirstClinicVisit, StageRole.Lead, new(2, 7), default, 30, 30, new(3, 9), DayKind.Clinic),
            new(MedicinesReview, StageRole.Pharmacist, new(1, 4), new(0.2, 1.5), 20, 20, new(2, 6), DayKind.Clinic, VisitChance: 0.5),
            new(FollowUp, StageRole.Lead, new(10, 20), new(0.1, 0.5), 20, 20, new(1, 4), DayKind.Clinic, VisitChance: 0.6),
            new(DischargeLetter, StageRole.Lead, new(0.1, 1.5), new(0.05, 0.5)),
        ], DueWeeks: 8, UrgentDueWeeks: 4, [(Priority.Low, 45), (Priority.Medium, 45), (Priority.High, 10)]),

        new(PathwayKind.PostMenopausalBleeding, "Post-menopausal bleeding pathway",
        [
            new(ReferralReceived, StageRole.Coordinator, new(0, 0.5), new(0.05, 0.3)),
            new(Triage, StageRole.Lead, new(0.2, 2), new(0.05, 0.5)),
            new(FirstClinicVisit, StageRole.Lead, new(1, 5), default, 45, 45, new(3, 10), DayKind.Clinic),
            new(BiopsyOrSample, StageRole.Lead, new(0.3, 2), default, 30, 30, new(2, 8), DayKind.Clinic),
            new(Histology, StageRole.Lab, new(0.3, 1), new(4, 9)),
            new(ResultsAndTeamReview, StageRole.Lead, new(0.5, 3), new(0.2, 2), 20, 20, new(2, 7), DayKind.Clinic, VisitChance: 0.4),
            new(DischargeLetter, StageRole.Lead, new(0.2, 2), new(0.1, 1)),
        ], DueWeeks: 6, UrgentDueWeeks: 3, [(Priority.Medium, 25), (Priority.High, 30), (Priority.Urgent, 45)]),

        new(PathwayKind.Prostate, "Prostate assessment",
        [
            new(ReferralReceived, StageRole.Coordinator, new(0, 1), new(0.05, 0.5)),
            new(Triage, StageRole.Lead, new(0.5, 4), new(0.05, 0.5)),
            new(BloodAndUrineTests, StageRole.Samples, new(2, 8), default, 15, 15, new(2, 8)),
            new(LabTests, StageRole.Lab, new(0.1, 0.5), new(1, 3)),
            new(FirstClinicVisit, StageRole.Lead, new(8, 24), default, 30, 30, new(7, 18), DayKind.Clinic),
            new(ResultsAndTeamReview, StageRole.Lead, new(2, 8), new(0.5, 3), 20, 20, new(3, 10), DayKind.Clinic, VisitChance: 0.5),
            new(FollowUp, StageRole.Lead, new(20, 34), new(0.2, 1), 20, 20, new(3, 10), DayKind.Clinic, VisitChance: 0.5),
            new(DischargeLetter, StageRole.Lead, new(0.5, 3), new(0.1, 1)),
        ], DueWeeks: 12, UrgentDueWeeks: 6, [(Priority.Low, 15), (Priority.Medium, 50), (Priority.High, 30), (Priority.Urgent, 5)]),

        new(PathwayKind.Diabetes, "Diabetes review",
        [
            new(ReferralReceived, StageRole.Coordinator, new(0, 1), new(0.05, 0.5)),
            new(BloodAndUrineTests, StageRole.Samples, new(1, 5), default, 15, 15, new(2, 7)),
            new(LabTests, StageRole.Lab, new(0.1, 0.5), new(1, 3)),
            new(ClinicReview, StageRole.Lead, new(2, 7), default, 30, 30, new(3, 10), DayKind.Clinic),
            new(MedicinesReview, StageRole.Pharmacist, new(0.5, 4), new(0.2, 1.5), 20, 20, new(2, 6), DayKind.Clinic, VisitChance: 0.5),
            new(FollowUp, StageRole.Nurse, new(14, 28), new(0.1, 1), 20, 20, new(2, 6), DayKind.Clinic, VisitChance: 0.4),
            new(DischargeLetter, StageRole.Lead, new(0.2, 2), new(0.1, 1)),
        ], DueWeeks: 10, UrgentDueWeeks: 4, RoutinePriorities),

        new(PathwayKind.Biomechanics, "Biomechanics course",
        [
            new(ReferralReceived, StageRole.Coordinator, new(0, 1), new(0.05, 0.5)),
            new(FirstClinicVisit, StageRole.Lead, new(1, 5), default, 45, 45, new(2, 7), DayKind.Clinic),
            new(BiomechanicsSession, StageRole.Lead, new(0.5, 2), default, 30, 30, new(5, 8), DayKind.Clinic),
            new(BiomechanicsSession, StageRole.Lead, new(0.2, 1), default, 30, 30, new(8, 12), DayKind.Clinic),
            new(BiomechanicsSession, StageRole.Lead, new(0.2, 1), default, 30, 30, new(10, 16), DayKind.Clinic),
            new(DischargeLetter, StageRole.Lead, new(0.2, 2), new(0.1, 1)),
        ], DueWeeks: 12, UrgentDueWeeks: 6, [(Priority.Low, 40), (Priority.Medium, 55), (Priority.High, 5)]),
    ];

    public static PathwayKindSpec For(PathwayKind pathwayKind) => PathwayKinds.Single(p => p.PathwayKind == pathwayKind);
}
