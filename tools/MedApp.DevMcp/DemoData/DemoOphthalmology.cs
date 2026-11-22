using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// The Eye clinic's templates, as the light demo has them: the four in <c>docs/pathways/ophthalmology.yaml</c> (a draft
/// not yet reviewed by a clinician), copied below field for field and mapped onto the model by <see cref="DemoTemplates"/>
/// as Dermatology's are.
/// </summary>
public static class DemoOphthalmology
{
    public const string CataractAssessment = "Cataract assessment";
    public const string GlaucomaAssessment = "Glaucoma assessment";
    public const string RedEyeAssessment = "Red eye assessment";
    public const string MacularAssessment = "Macular assessment";

    /// <summary>
    /// The library's descriptions for the stages only the Eye clinic's templates share (Triage, the final letter and the
    /// lab's Microbiology keep Dermatology's, the stage library's first).
    /// </summary>
    private static readonly Dictionary<string, string> SharedDescriptions = new()
    {
        ["Refer to hospital eye service"] = "Refer to the hospital eye service for specialist care or treatment.",
        ["Urgent hospital eye referral"] = "Urgent referral to the hospital eye service; phone the team first when it’s needed the same day.",
    };

    /// <summary>The Eye clinic's templates, for <see cref="DemoTemplates"/>.</summary>
    public static ReviewedClinic Clinic => new(DemoPeople.EyeClinic, Reviewed, SharedDescriptions);

    /// <summary>The Eye clinic's templates: a copy of <c>docs/pathways/ophthalmology.yaml</c>.</summary>
    private static readonly ReviewedTemplate[] Reviewed =
    [
        new(
            CataractAssessment,
            RetentionClass.Standard,
            "triage",
            [
                new("triage", "Triage", ReviewedKind.Step, VisitRequirement.None, ["assessment"], null, "Referral from the optometrist: record the visual acuity given and the reason for referral.", Snomed: "225390008 | Triage"),
                new("assessment", "Cataract assessment", ReviewedKind.Assessment, VisitRequirement.Required, null, [("Wants surgery and suitable", ["biometry"]), ("Not affecting daily life", ["discharge"]), ("Other eye problem found", ["refer-hospital"])], "Slit lamp and dilated examination; talk through the benefits and risks of surgery. The patient decides whether they want it."),
                new("biometry", "Biometry", ReviewedKind.Step, VisitRequirement.Required, ["refer-surgery"], null, "Measurements for the lens implant, taken as the hospital asks, recorded as measured."),
                new("refer-surgery", "Refer for cataract surgery", ReviewedKind.Step, VisitRequirement.None, [], null, "Referral for the operation, with the biometry and which eye.", NeedsLetter: "Referral letter to hospital", Snomed: "306206005 | Referral to service"),
                new("discharge", "Final letter to GP", ReviewedKind.Step, VisitRequirement.None, [], null, "Cataract not affecting daily life: advice to return through the optometrist if vision changes.", NeedsLetter: "Final letter to GP", Snomed: "58000006 | Patient discharge"),
                new("refer-hospital", "Refer to hospital eye service", ReviewedKind.Step, VisitRequirement.None, [], null, "Another eye problem found at the assessment, for the hospital eye service.", NeedsLetter: "Referral letter to hospital", Snomed: "306206005 | Referral to service"),
            ],
            []),
        new(
            GlaucomaAssessment,
            RetentionClass.Standard,
            "triage",
            [
                new("triage", "Triage", ReviewedKind.Step, VisitRequirement.None, ["glaucoma-assessment"], null, "Referral from the optometrist: record the pressures and findings they gave.", Snomed: "225390008 | Triage"),
                new("glaucoma-assessment", "Glaucoma assessment", ReviewedKind.Assessment, VisitRequirement.Required, null, [("No glaucoma", ["discharge"]), ("Ocular hypertension", ["monitoring-plan"]), ("Glaucoma", ["start-treatment"])], "Eye pressure, visual fields and optic nerve imaging, recorded as measured. The diagnosis is the clinician's."),
                new("start-treatment", "Start eye drops", ReviewedKind.Step, VisitRequirement.Suggested, ["review"], null, "Drops chosen and explained by the clinician; the GP is asked to prescribe them."),
                new("monitoring-plan", "Agree monitoring plan", ReviewedKind.Step, VisitRequirement.Suggested, ["review"], null, "How often pressures and fields are checked, and by whom."),
                new("review", "Glaucoma review", ReviewedKind.Assessment, VisitRequirement.Required, null, [("Stable", ["back-to-optometrist"]), ("Not controlled", ["refer-hospital"])], "Pressures and fields again, compared with the first visit."),
                new("back-to-optometrist", "Back to optometrist monitoring", ReviewedKind.Step, VisitRequirement.None, [], null, "Stable: the optometrist continues the monitoring plan; the GP is told.", NeedsLetter: "Final letter to GP", Snomed: "58000006 | Patient discharge"),
                new("discharge", "Final letter to GP", ReviewedKind.Step, VisitRequirement.None, [], null, "No glaucoma found: advice to keep up routine sight tests.", NeedsLetter: "Final letter to GP", Snomed: "58000006 | Patient discharge"),
                new("refer-hospital", "Refer to hospital eye service", ReviewedKind.Step, VisitRequirement.None, [], null, "Not controlled on treatment, for the hospital eye service.", NeedsLetter: "Referral letter to hospital", Snomed: "306206005 | Referral to service"),
            ],
            []),
        new(
            RedEyeAssessment,
            RetentionClass.Standard,
            "triage",
            [
                new("triage", "Triage", ReviewedKind.Step, VisitRequirement.None, ["eye-exam"], null, "Same-week referral: record the symptoms, contact lens use and how long it has lasted. Sight-threatening signs go to the hospital the same day.", Snomed: "225390008 | Triage"),
                new("eye-exam", "Eye examination", ReviewedKind.Assessment, VisitRequirement.Required, null, [("Settling: advice", ["discharge"]), ("Swab needed", ["take-swab"]), ("Sight-threatening", ["refer-hospital-urgent"])], "Slit lamp examination, recorded as found."),
                new("take-swab", "Take eye swab", ReviewedKind.Step, VisitRequirement.Required, ["microbiology"], null, "A swab of the eye, labelled with which eye, to the lab."),
                new("microbiology", "Microbiology", ReviewedKind.Step, VisitRequirement.None, ["microbiology-result"], null, "The lab cultures the swab and enters the report.", Lab: true),
                new("microbiology-result", "Microbiology result", ReviewedKind.LabResult, VisitRequirement.Suggested, null, [("No growth", ["discharge"]), ("Bacterial infection", ["treatment-plan"]), ("Unusual organism", ["refer-hospital-urgent"])], "The clinician records the result as the lab reported it."),
                new("treatment-plan", "Agree treatment plan", ReviewedKind.Step, VisitRequirement.Suggested, ["discharge"], null, "Treatment chosen by the clinician; the GP is asked to prescribe it."),
                new("discharge", "Final letter to GP", ReviewedKind.Step, VisitRequirement.None, [], null, "Settled or treated: advice on when to seek help again.", NeedsLetter: "Final letter to GP", Snomed: "58000006 | Patient discharge"),
                new("refer-hospital-urgent", "Urgent hospital eye referral", ReviewedKind.Step, VisitRequirement.None, [], null, "Sight-threatening or an unusual organism: same-day referral to the hospital eye service.", NeedsLetter: "Urgent referral letter to hospital", Snomed: "306206005 | Referral to service"),
            ],
            []),
        new(
            MacularAssessment,
            RetentionClass.Standard,
            "triage",
            [
                new("triage", "Triage", ReviewedKind.Step, VisitRequirement.None, ["oct"], null, "Referral from the optometrist: record the symptoms and when they started. Sudden distortion is seen fast.", Snomed: "225390008 | Triage"),
                new("oct", "Macular OCT assessment", ReviewedKind.Assessment, VisitRequirement.Required, null, [("Dry macular degeneration: advice", ["discharge"]), ("Suspected wet macular degeneration", ["refer-hospital-urgent"]), ("Diabetic macular change", ["refer-hospital"])], "Scan of the macula and examination, recorded as found. The diagnosis is the clinician's."),
                new("discharge", "Final letter to GP", ReviewedKind.Step, VisitRequirement.None, [], null, "Dry macular degeneration: advice and an Amsler chart to watch for change.", NeedsLetter: "Final letter to GP", Snomed: "58000006 | Patient discharge"),
                new("refer-hospital", "Refer to hospital eye service", ReviewedKind.Step, VisitRequirement.None, [], null, "Diabetic macular change, for the hospital eye service.", NeedsLetter: "Referral letter to hospital", Snomed: "306206005 | Referral to service"),
                new("refer-hospital-urgent", "Urgent hospital eye referral", ReviewedKind.Step, VisitRequirement.None, [], null, "Suspected wet macular degeneration: fast-track referral for treatment.", NeedsLetter: "Urgent referral letter to hospital", Snomed: "306206005 | Referral to service"),
            ],
            []),
    ];
}
