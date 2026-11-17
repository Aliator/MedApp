using System.Globalization;
using System.Text;

namespace MedApp.DevMcp.DemoData;

/// <summary>What a stage note is about, beyond the stage: the procedure, which side, and where it is in a course.</summary>
/// <param name="Kind">The kind of procedure.</param>
/// <param name="Side">"left" or "right" for a procedure with a side, else null.</param>
/// <param name="Urgent">An urgent (two-week-wait) referral.</param>
/// <param name="Occurrence">Which time this stage comes round in the procedure (1 for the first), e.g. the second consultation.</param>
/// <param name="Occurrences">How many times this stage comes round in the procedure.</param>
public sealed record NoteContext(ProcedureKind Kind, string? Side, bool Urgent, int Occurrence = 1, int Occurrences = 1);

/// <summary>
/// Builds the free text staff write, from parts: stage notes that depend on the stage and the kind of procedure, with
/// synthetic observations, measurements, results in words, medicine changes and safety-netting; and the reasons and
/// notes around them (blocking, cancelling, appointments, corrections). Each note is put together from several
/// choices and numbers, so two procedures of the same kind don't read the same. Notes say "the patient", never a
/// name, and mention no real people, places, organisations or brands (only generic medicines).
/// </summary>
public static class NoteBuilder
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>A finished stage's notes: a sentence to a short paragraph, sometimes with a list or a bold point.</summary>
    public static string StageNote(string stage, NoteContext context, Random random)
    {
        var parts = stage switch
        {
            "Referral received" => Referral(context, random),
            "Triage" => Triage(context, random),
            "Consultation" => context.Occurrence > 1 ? SecondConsultation(context, random) : Consultation(context, random),
            "Pre-assessment" => PreAssessment(context, random),
            "Consent" => Consent(context, random),
            "Scheduling" => Scheduling(context, random),
            "Procedure" => Procedure(context, random),
            "Recovery" => Recovery(context, random),
            "Wound check" => WoundCheck(context, random),
            "Post-procedure review" => PostProcedureReview(context, random),
            "Results review" => ResultsReview(context, random),
            "Follow-up" => FollowUp(context, random),
            "Physiotherapy session" => PhysioSession(context, random),
            "Discharge" => Discharge(context, random),
            _ => [$"Completed. Nothing further to add."],
        };

        return Compose(parts, random);
    }

    /// <summary>A note started on a stage still under way: what's been done so far, or what it's waiting for.</summary>
    public static string ProgressNote(string stage, NoteContext context, Random random) => stage switch
    {
        "Pre-assessment" => Pick(random,
            "Questionnaire returned. Bloods requested; awaiting results before the face-to-face assessment.",
            $"Started by phone: medication list taken. {MedicineChange(random)}",
            "Awaiting the GP summary before the assessment."),
        "Scheduling" => Pick(random,
            "Waiting for a theatre slot; patient happy to take a short-notice date.",
            "Two dates offered by phone; patient to call back.",
            "On the list for the next available slot. Preparation pack ready to post."),
        "Consultation" => Pick(random,
            $"Referral reviewed ahead of clinic: {Pick(random, context.Kind.Indications)}.",
            "GP letter and previous results reviewed before the appointment."),
        "Consent" => "Information leaflet sent; consent to be confirmed at the next visit.",
        "Results review" => Pick(random,
            "Awaiting histology.",
            "Histology reported; to discuss with the patient by phone.",
            "Results back; call booked with the patient."),
        "Physiotherapy session" => $"Home exercise sheet sent ahead of session {context.Occurrence}.",
        "Triage" => "Referral reviewed; awaiting the GP's blood results before deciding the route.",
        _ => Pick(random, "Started; notes to follow.", "In progress.", "Working on this today."),
    };

    /// <summary>The note after an admin reopened a stage: the original, and a correction below it.</summary>
    public static string Correction(string original, NoteContext context, Random random)
    {
        var fix = Pick(random,
            $"Allergy status updated: penicillin allergy (rash) was missed in the first entry.",
            $"Weight corrected to {random.Next(62, 96)} kg (typed in error); BMI recalculated as {random.Next(22, 33)}.",
            $"Medication list updated: also takes amlodipine 5 mg once daily.",
            context.Side is { } side
                ? $"Side corrected: this is the {side} side, as on the consent form."
                : "Date of the previous procedure corrected from the GP letter.");
        return $"{original}\n\n**Correction:** {fix}";
    }

    /// <summary>A procedure's own notes, when it's created (often empty).</summary>
    public static string ProcedureNote(NoteContext context, Random random)
    {
        if (random.NextDouble() < 0.35)
            return string.Empty;

        return Pick(random,
            "Patient prefers a morning appointment.",
            $"Interpreter needed ({Pick(random, "Polish", "Urdu", "Romanian", "Portuguese", "Arabic")}).",
            "Lives alone: confirm an escort and someone at home for the first night.",
            "Takes apixaban: anticoagulation plan needed before the procedure.",
            "Takes warfarin: INR check and bridging plan to agree with the GP.",
            "Previous difficult cannulation noted.",
            "Type 2 diabetes on metformin: first on the list if possible.",
            "Hard of hearing: please speak clearly, face to face.",
            "Carer attends appointments with the patient.",
            $"Referred for {Pick(random, context.Kind.Indications)}.");
    }

    /// <summary>An edit to a procedure's notes: what it said, with an update added.</summary>
    public static string ProcedureNoteUpdate(string current, Random random)
    {
        var update = Pick(random,
            "Update: patient now has an escort arranged.",
            "Update: anticoagulation plan agreed with the GP.",
            "Update: prefers telephone contact after 10:00.",
            "Update: transport booked for the day.",
            "Update: GP summary received and scanned.");
        return string.IsNullOrEmpty(current) ? update : $"{current} {update}";
    }

    public static string BlockReason(Random random) => Pick(random,
        "Waiting for the GP to confirm the anticoagulation plan.",
        "Patient unwell with a chest infection; on hold until recovered.",
        "Abnormal pre-op ECG: waiting for a cardiology opinion.",
        "HbA1c 79 mmol/mol: on hold until diabetes control improves.",
        "Awaiting an interpreter for the consent discussion.",
        "Patient asked to pause for a few weeks for family reasons.");

    public static string CancelReason(Random random) => Pick(random,
        "Patient chose not to go ahead after discussing the options.",
        "Symptoms resolved; procedure no longer needed.",
        "Duplicate referral: already listed under another procedure.",
        "Patient moved out of the area; care transferred.",
        "Not fit for an anaesthetic; managed conservatively instead.",
        "Patient opted for treatment elsewhere.");

    /// <summary>Why an admin finished a stage that needs a visit without one: the rare, audited bypass.</summary>
    public static string BypassReason(Random random) => Pick(random,
        "Pre-assessment completed by phone: the patient was abroad until the week before surgery. GP bloods and ECG reviewed.",
        "Seen on the ward during an unplanned admission; no separate clinic visit needed.");

    /// <summary>A note on an appointment, as the person booking it might add.</summary>
    public static string AppointmentNote(Random random) => Pick(random,
        "Patient prefers morning appointments.",
        "Needs step-free access.",
        "Interpreter booked.",
        "Patient will bring an up-to-date medication list.",
        "Phone reminder requested the day before.",
        "Hard of hearing: text reminder only.",
        "Carer will attend with the patient.",
        "Transport booked.");

    /// <summary>An appointment note edited later.</summary>
    public static string AppointmentNoteUpdate(string current, Random random) => current + " " + Pick(random,
        "Confirmed by phone.",
        "Patient asked for the first slot of the day.",
        "Now bringing a relative instead.",
        "Reminder sent.");

    public static string PatientCancelReason(Random random) => Pick(random,
        "Patient called to cancel: unwell.",
        "Patient called to cancel: work commitment; asked for a later date.",
        "Patient called to cancel: no transport on the day.",
        "Patient called to cancel: caring for a relative that day.");

    public static string ClinicianCancelReason(Random random) => Pick(random,
        "Clinician unavailable (sickness); patient informed and offered a new date.",
        "Clinic cancelled for mandatory training; patient informed.",
        "Clinician called to theatre; patient informed and rebooked.");

    public const string ProcedureCancelledAppointmentReason = "Procedure cancelled; patient informed by phone.";

    // Stage notes ----------------------------------------------------------------------------------------------

    private static List<string> Referral(NoteContext c, Random r)
    {
        var parts = new List<string>
        {
            Pick(r, "Referral letter from the GP received and logged.", "GP referral received and checked.", "Referral received; demographics checked against the GP record."),
            $"Reason: {Pick(r, c.Kind.Indications)}.",
        };
        if (c.Urgent)
            parts.Add("**Two-week-wait referral:** flagged for urgent triage.");
        else if (r.NextDouble() < 0.4)
            parts.Add(Pick(r, "Routine priority.", "Recent bloods attached to the referral.", "Previous imaging requested from the GP."));
        return parts;
    }

    private static List<string> Triage(NoteContext c, Random r) =>
    [
        $"Triaged as {(c.Urgent ? "urgent" : Pick(r, "routine", "soon"))}.",
        Pick(r,
            $"Clinic consultation first: {Pick(r, "on anticoagulants", "several co-morbidities to review", "symptoms need a history before the test")}.",
            "Straight-to-test not appropriate; clinic first.",
            $"Suitable for {c.Kind.Short} after a clinic review."),
        r.NextDouble() < 0.5 ? $"Bloods: Hb {r.Next(104, 150)} g/L, ferritin {r.Next(6, 120)} micrograms/L." : "No further tests needed before clinic.",
    ];

    private static List<string> Consultation(NoteContext c, Random r)
    {
        var parts = new List<string>
        {
            $"Seen in clinic with {Pick(r, "a three-month", "a six-month", "a year's", "a few weeks'")} history of {Pick(r, c.Kind.Indications)}.",
        };
        if (r.NextDouble() < 0.6)
            parts.Add($"BP {Bp(r)}, HR {r.Next(58, 96)}.");
        if (c.Side is { } side && r.NextDouble() < 0.7)
            parts.Add($"Examination of the {side} side consistent with the referral.");
        parts.Add(Pick(r,
            $"Options discussed, including watchful waiting. Patient would like to go ahead with {c.Kind.Short}.",
            $"Explained what {c.Kind.Short} involves and the recovery. Keen to proceed.",
            $"Discussed the benefits and risks of {c.Kind.Short} versus carrying on as now; patient chose to proceed."));
        parts.Add(Plan(r,
            "Plan:",
            "Pre-assessment to be booked",
            $"Leaflet on {c.Kind.Short} given",
            "GP letter sent",
            r.NextDouble() < 0.5 ? MedicineChange(r) : "No medication changes"));
        return parts;
    }

    private static List<string> SecondConsultation(NoteContext c, Random r) =>
    [
        Pick(r, "Reviewed in clinic after pre-assessment.", "Seen again to go through the pre-assessment results.", "Follow-up consultation before listing."),
        Pick(r,
            $"Bloods and ECG satisfactory. Happy to proceed with {c.Kind.Short}.",
            $"Anaesthetic review completed; fit for {c.Kind.Anaesthetic}.",
            "Weight down 3 kg since the first visit. Good progress."),
        r.NextDouble() < 0.5 ? "**Listed** for surgery; consent to follow." : "Listed. Questions about time off work answered.",
    ];

    private static List<string> PreAssessment(NoteContext c, Random r)
    {
        var observations = $"BP {Bp(r)}, HR {r.Next(56, 98)}, SpO2 {r.Next(95, 101)}% on air, BMI {r.Next(21, 37)}.";
        var parts = new List<string> { Pick(r, "Pre-assessment completed in clinic.", "Seen for pre-assessment.", "Face-to-face pre-assessment.") };

        if (r.NextDouble() < 0.4)
        {
            parts.Add(Plan(r, "Checks:",
                observations,
                $"Bloods: FBC, U&E{(r.NextDouble() < 0.5 ? ", group and save" : "")} sent",
                $"ECG: sinus rhythm, rate {r.Next(58, 90)}",
                $"Allergies: {Pick(r, "none known", "penicillin (rash)", "plasters", "codeine (nausea)")}"));
        }
        else
        {
            parts.Add(observations);
            parts.Add($"ECG sinus rhythm. Allergies: {Pick(r, "none known", "penicillin (rash)", "latex", "codeine (nausea)")}.");
        }

        parts.Add(MedicineChange(r));
        parts.Add(Pick(r,
            $"Fit for {c.Kind.Anaesthetic}.",
            $"**Fit for {c.Kind.Anaesthetic}** once bloods are back.",
            $"Fit for {c.Kind.Anaesthetic}; MRSA screen taken."));
        if (c.Kind.Prep.Length > 0)
            parts.Add($"Told to {Pick(r, c.Kind.Prep)}.");
        return parts;
    }

    private static List<string> Consent(NoteContext c, Random r)
    {
        string[] risks = c.Kind.Risks.Length == 0 ? ["discomfort"] : c.Kind.Risks.OrderBy(_ => r.Next()).Take(r.Next(2, 4)).ToArray();
        return
        [
            $"Consent form signed for {(c.Side is { } side ? $"{side} " : "")}{c.Kind.Short}.",
            r.NextDouble() < 0.35
                ? Plan(r, "Risks discussed:", risks)
                : $"Risks discussed: {string.Join(", ", risks)}.",
            Pick(r, "Questions answered; copy of the form given.", "Patient had time to think and confirmed by phone.", "Alternatives discussed, including no treatment."),
        ];
    }

    private static List<string> Scheduling(NoteContext c, Random r) =>
    [
        Pick(r,
            $"Booked onto a {Pick(r, "morning", "afternoon")} list.",
            "Date agreed by phone; admission letter sent.",
            "Offered two dates; patient chose the later one for work reasons.",
            "Short-notice slot accepted."),
        c.Kind.Prep.Length > 0 ? $"Instructions sent: {Pick(r, c.Kind.Prep)}." : "Preparation instructions sent.",
        r.NextDouble() < 0.3 ? "Transport booked." : string.Empty,
    ];

    private static List<string> Procedure(NoteContext c, Random r)
    {
        var parts = new List<string>
        {
            $"{(c.Side is { } side ? $"{Capitalise(side)} {c.Kind.Short}" : Capitalise(c.Kind.Short))} under {c.Kind.Anaesthetic}.",
            Pick(r, c.Kind.Findings),
        };
        if (c.Kind.Anaesthetic == "sedation" || c.Kind.Anaesthetic.Contains("sedation if wanted", StringComparison.Ordinal))
            parts.Add($"Midazolam {Pick(r, "1", "2", "3")} mg{(r.NextDouble() < 0.5 ? $", fentanyl {Pick(r, "25", "50")} micrograms" : "")}.");
        parts.Add(r.NextDouble() < 0.9
            ? Pick(r, "Uneventful.", "No complications.", "Tolerated well.")
            : "**Minor bleeding** controlled at the time; observed for an extra hour.");
        return parts;
    }

    private static List<string> Recovery(NoteContext c, Random r) =>
    [
        $"Obs stable: BP {Bp(r)}, HR {r.Next(58, 92)}, SpO2 {r.Next(95, 100)}%, temp {Temp(r)}.",
        Pick(r, "Eating and drinking; passed urine.", "Pain controlled with paracetamol.", "Mobilised to the toilet independently."),
        Pick(r, "Discharged home with an escort.", "Discharged home; partner collecting.", "Discharged with a relative."),
        SafetyNet(r),
    ];

    private static List<string> WoundCheck(NoteContext c, Random r) =>
    [
        Pick(r, "Wound clean and dry.", "Wound healing well; edges together.", "Slight redness at one end; no discharge."),
        Pick(r, "Sutures removed.", "Steri-strips removed.", "Clips removed.", "Dressing changed."),
        $"Temp {Temp(r)}.",
        r.NextDouble() < 0.2 ? "Swab taken as a precaution." : "No signs of infection.",
    ];

    private static List<string> PostProcedureReview(NoteContext c, Random r) =>
    [
        $"Reviewed {Pick(r, "six", "eight", "ten")} weeks after surgery.",
        Pick(r, c.Kind.Results),
        Pick(r, "Discharged back to the GP.", "Review again in three months.", "To continue physiotherapy; review if not improving."),
    ];

    private static List<string> ResultsReview(NoteContext c, Random r) =>
    [
        Pick(r, c.Kind.Results),
        Pick(r, "Discussed with the patient by phone.", "Results explained to the patient in clinic.", "Letter sent to the patient and the GP."),
        r.NextDouble() < 0.3 ? MedicineChange(r) : string.Empty,
    ];

    private static List<string> FollowUp(NoteContext c, Random r) =>
    [
        Pick(r, "Reviewed by phone.", "Seen in the nurse-led clinic.", "Follow-up call."),
        Pick(r,
            $"Recovering well after {c.Kind.Short}; pain {r.Next(0, 4)}/10.",
            "Back to normal activities.",
            $"Wound healed. {Pick(r, c.Kind.Results)}"),
        Pick(r, "Discharged back to the GP.", "No further follow-up needed.", SafetyNet(r)),
    ];

    private static List<string> PhysioSession(NoteContext c, Random r)
    {
        var part = c.Kind.Short;
        var measure = part switch
        {
            "shoulder" => $"flexion {r.Next(100, 170)} degrees, external rotation {r.Next(25, 70)} degrees",
            "knee" => $"flexion {r.Next(85, 125)} degrees, extension lag {r.Next(0, 10)} degrees",
            "ankle" => $"single-leg balance {r.Next(5, 31)} seconds",
            _ => $"forward bend to {Pick(r, "mid-shin", "ankles", "knees")}, SLR {r.Next(50, 85)} degrees",
        };
        var exercise = part switch
        {
            "shoulder" => Pick(r, "band external rotation", "wall slides", "scapular setting"),
            "knee" => Pick(r, "step-ups", "mini squats", "static bike 10 minutes"),
            "ankle" => Pick(r, "heel raises", "balance board", "hopping drills"),
            _ => Pick(r, "bridging", "cat-camel", "walking programme"),
        };
        return
        [
            $"Session {c.Occurrence} of {c.Occurrences}.",
            $"Pain {r.Next(1, 7)}/10; {measure}.",
            $"Exercises progressed: {exercise}.",
            r.NextDouble() < 0.4 ? "**Home programme** reviewed; doing it most days." : "Home programme reviewed.",
        ];
    }

    private static List<string> Discharge(NoteContext c, Random r) =>
    [
        $"Discharged from physiotherapy after {c.Occurrences} sessions.",
        Pick(r, c.Kind.Results),
        "Advised to keep up the home exercises; can self-refer within six months.",
    ];

    // Parts ---------------------------------------------------------------------------------------------------

    private static string MedicineChange(Random r) => Pick(r,
        "Medication reviewed: omit apixaban 48 h before.",
        "Medication reviewed: stop clopidogrel 7 days before, as agreed with the GP.",
        "Medication reviewed: omit metformin on the morning of the procedure.",
        "Medication reviewed: hold ramipril on the day.",
        "Medication reviewed: warfarin to stop 5 days before; INR check the day before.",
        "Medication reviewed: no changes needed.",
        "Medication reviewed: continue all usual medicines.");

    private static string SafetyNet(Random r) => Pick(r,
        "Safety-netting given: fever, spreading redness or increasing pain → contact the unit.",
        "Safety-netting advice given for bleeding, fever or worsening pain.",
        "Written advice given on what to look out for and who to call out of hours.");

    private static string Bp(Random r) => $"{r.Next(106, 166)}/{r.Next(62, 96)}";

    private static string Temp(Random r) => (36.2 + r.Next(0, 13) / 10.0).ToString("0.0", Invariant);

    /// <summary>Some lines as a short Markdown list after a heading line; otherwise a sentence.</summary>
    private static string Plan(Random r, string heading, params string[] items)
    {
        var listed = items.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.TrimEnd('.')).ToList();
        return r.NextDouble() < 0.45
            ? heading + "\n" + string.Join("\n", listed.Select(i => $"- {i}"))
            : $"{heading} {string.Join("; ", listed.Select(Lower))}.";
    }

    private static string Compose(IEnumerable<string> parts, Random random)
    {
        // Sentences run on; a list stands apart, with a blank line before and after it.
        var text = new StringBuilder();
        var previousWasList = false;
        foreach (var part in parts.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            var isList = part.Contains('\n');
            if (text.Length > 0)
                text.Append(isList || previousWasList ? "\n\n" : " ");
            text.Append(part);
            previousWasList = isList;
        }

        return text.ToString();
    }

    private static string Lower(string s) =>
        s.Length > 1 && char.IsUpper(s[0]) && !char.IsUpper(s[1]) ? char.ToLowerInvariant(s[0]) + s[1..] : s;

    private static string Capitalise(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static T Pick<T>(Random random, params T[] items) => items[random.Next(items.Length)];
}
