using System.Text;

namespace MedApp.DevMcp.DemoData;

/// <summary>What a stage note is about, beyond the stage: the referral, its site, and where it is in a course.</summary>
/// <param name="Kind">The kind of referral.</param>
/// <param name="Side">Where on the body ("left forearm"), for a kind with a site, else null.</param>
/// <param name="Urgent">An urgent referral.</param>
/// <param name="Occurrence">Which time this stage comes round in the pathway (1 for the first), e.g. the second session.</param>
/// <param name="Occurrences">How many times this stage comes round in the pathway.</param>
/// <param name="Outcome">Which of the kind's outcomes this referral has, so its notes agree from stage to stage.</param>
public sealed record NoteContext(ProcedureKind Kind, string? Side, bool Urgent, int Occurrence = 1, int Occurrences = 1, int Outcome = 0)
{
    /// <summary>How this referral turns out: what was found, what the lab said and the plan.</summary>
    public KindOutcome Result => Kind.Outcomes[Outcome % Kind.Outcomes.Length];
}

/// <summary>
/// Builds the free text staff write, from parts: stage notes that depend on the stage and the kind of referral, with
/// synthetic lesion descriptions, biopsy sites, measurements, results in words, plans and safety-netting; and the
/// reasons and notes around them (blocking, cancelling, appointments, corrections). Each note is put together from
/// several choices and numbers, so two referrals of the same kind don't read the same, while one referral's notes
/// agree with each other (its site and its outcome are chosen once). Notes say "the patient", never a name, and
/// mention no real people, places, organisations or brands (only generic medicines).
/// </summary>
public static class NoteBuilder
{
    /// <summary>A finished stage's notes: a sentence to a short paragraph, sometimes with a list or a bold point.</summary>
    public static string StageNote(string stage, NoteContext context, Random random)
    {
        var parts = stage switch
        {
            DemoPathwayKinds.ReferralReceived => Referral(context, random),
            DemoPathwayKinds.Triage => Triage(context, random),
            DemoPathwayKinds.FirstClinicVisit => FirstClinicVisit(context, random),
            DemoPathwayKinds.BiopsyOrSample => BiopsyOrSample(context, random),
            DemoPathwayKinds.Histology => Histology(context, random),
            DemoPathwayKinds.ResultsAndTeamReview => ResultsAndTeamReview(context, random),
            DemoPathwayKinds.Treatment => Treatment(context, random),
            DemoPathwayKinds.FollowUp => FollowUp(context, random),
            DemoPathwayKinds.DischargeLetter => DischargeLetter(context, random),
            DemoPathwayKinds.BloodAndUrineTests => BloodAndUrineTests(context, random),
            DemoPathwayKinds.LabTests => LabTests(context, random),
            DemoPathwayKinds.ClinicReview => ClinicReview(context, random),
            DemoPathwayKinds.MedicinesReview => MedicinesReview(context, random),
            DemoPathwayKinds.BiomechanicsSession => BiomechanicsSession(context, random),
            _ => ["Completed. Nothing further to add."],
        };

        return Compose(parts);
    }

    /// <summary>A note started on a stage still under way: what's been done so far, or what it's waiting for.</summary>
    public static string ProgressNote(string stage, NoteContext context, Random random) => stage switch
    {
        DemoPathwayKinds.Triage => "Referral read; waiting for the GP's medication list before choosing the clinic.",
        DemoPathwayKinds.FirstClinicVisit => Pick(random,
            $"Referral reviewed ahead of clinic: {Pick(random, context.Kind.Indications)}.",
            "GP letter and earlier results reviewed before the visit."),
        DemoPathwayKinds.BiopsyOrSample => "Information leaflet sent; consent to be confirmed on the day.",
        DemoPathwayKinds.Histology => Pick(random,
            "Specimen booked in; processing.",
            "In processing: slides due tomorrow.",
            "Waiting for extra levels before reporting."),
        DemoPathwayKinds.ResultsAndTeamReview => Pick(random,
            "On the list for the next team meeting.",
            "Report back; call booked with the patient.",
            "Waiting for the report."),
        DemoPathwayKinds.Treatment => "Listed for the next minor surgery list; aftercare leaflet sent.",
        DemoPathwayKinds.BloodAndUrineTests => "Request forms printed; patient asked to bring a urine sample.",
        DemoPathwayKinds.LabTests => "Samples booked in; on the afternoon run.",
        DemoPathwayKinds.ClinicReview => "Results back; to go through them at the visit.",
        DemoPathwayKinds.MedicinesReview => "Medication list asked for from the GP surgery.",
        DemoPathwayKinds.BiomechanicsSession => $"Home programme sheet sent ahead of session {context.Occurrence}.",
        _ => Pick(random, "Started; notes to follow.", "In progress.", "Working on this today."),
    };

    /// <summary>The note after an admin reopened a stage: the original, and a correction below it.</summary>
    public static string Correction(string original, NoteContext context, Random random)
    {
        var fix = Pick(random,
            "Allergy status updated: penicillin allergy (rash) was missed in the first entry.",
            $"Weight corrected to {random.Next(62, 96)} kg (typed in error); BMI recalculated as {random.Next(22, 33)}.",
            "Medication list updated: also takes amlodipine 5 mg once a day.",
            context.Side is { } site
                ? $"Site corrected: this is the {site}, as on the referral and the request form."
                : "Date of the earlier clinic letter corrected from the GP's copy.");
        return $"{original}\n\n**Correction:** {fix}";
    }

    /// <summary>A pathway record's own first note, when it's created (often empty).</summary>
    public static string PathwayNote(NoteContext context, Random random)
    {
        if (random.NextDouble() < 0.35)
            return string.Empty;

        return Pick(random,
            "Patient prefers a morning appointment.",
            $"Interpreter needed ({Pick(random, "Polish", "Urdu", "Romanian", "Portuguese", "Arabic")}).",
            "Needs step-free access and a ground-floor room.",
            "Takes apixaban: to note before any biopsy.",
            "Takes warfarin: INR needed in the week before any biopsy.",
            "Needle phobia: allow extra time for bloods.",
            "Type 2 diabetes on metformin.",
            "Hard of hearing: please speak clearly, face to face.",
            "Carer comes to appointments with the patient.",
            $"Referred with {Pick(random, context.Kind.Indications)}.");
    }

    /// <summary>An edit to a pathway record's note: what it said, with an update added.</summary>
    public static string PathwayNoteUpdate(string current, Random random)
    {
        var update = Pick(random,
            "Update: transport booked for the visit.",
            "Update: interpreter booked.",
            "Update: prefers telephone contact after 10:00.",
            "Update: carer's phone number added to the record.",
            "Update: GP summary received and scanned.");
        return string.IsNullOrEmpty(current) ? update : $"{current} {update}";
    }

    /// <summary>
    /// A note added to a pathway record after a stage, by whoever finished it: what happens next, for the rest of the
    /// team. Synthetic, and about "the patient", never a name.
    /// </summary>
    public static string PathwayFollowUpNote(string stage, StageRole role, Random random) => role switch
    {
        StageRole.Coordinator => Pick(random,
            $"{stage} done. Letter sent to the patient with the next steps.",
            $"{stage} done. Patient called to confirm; prefers contact by phone after 10:00.",
            "Patient asked for a copy of the clinic letter: posted today."),
        StageRole.Lab => Pick(random,
            "Report authorised; slides kept out for the team meeting.",
            "Result phoned to the clinician, as the request form asked.",
            $"Reported in {random.Next(2, 9)} working days. Block reference is on the report."),
        StageRole.Samples => Pick(random,
            "Bloods and urine taken; in the lab the same morning.",
            "Patient felt faint after the bloods: fine after ten minutes lying down. Advised to eat beforehand next time.",
            "Sample pot given for a repeat urine sample: to drop it in on Monday."),
        StageRole.Nurse => Pick(random,
            "Spoke to the patient: doing well, back to normal activities.",
            "Results gone through again with the patient by phone; letter to the GP to follow.",
            "Patient has the nurse specialist's phone line for any worries."),
        StageRole.Pharmacist => Pick(random,
            "Medication list in the record brought up to date.",
            "GP surgery told of the change by letter.",
            "Patient to phone if the new medicine doesn't suit them."),
        _ => Pick(random,
            $"{stage} done. Plan discussed with the patient, who is happy to go ahead.",
            "Discussed at the weekly team meeting: plan unchanged.",
            "GP letter dictated."),
    };

    /// <summary>A small fix to a note, by its author: a word corrected or a line added.</summary>
    public static string PathwayNoteFix(string current, Random random) =>
        $"{current} {Pick(random, "(Edited: date confirmed with the patient.)", "(Edited: GP informed.)", "(Edited: typo corrected.)")}";

    public static string BlockReason(Random random) => Pick(random,
        "Waiting for the GP to send the medication list and earlier clinic letters.",
        "Patient unwell with a chest infection; on hold until recovered.",
        "Waiting for an interpreter for the next visit.",
        "Patient asked to pause for a few weeks for family reasons.",
        "Patient abroad until next month; visits on hold.",
        "Waiting for records from the hospital that treated the patient before.");

    public static string CancelReason(Random random) => Pick(random,
        "Patient chose not to go ahead after discussing the options.",
        "Symptoms settled; no longer needed.",
        "Duplicate referral: already open under another pathway.",
        "Patient moved out of the area; care transferred.",
        "Referred on to the hospital service instead.",
        "Patient opted to be seen elsewhere.");

    /// <summary>Why an admin finished a stage that needs a visit without one: the rare, audited bypass.</summary>
    public static string BypassReason(Random random) => Pick(random,
        "Bloods and urine sample taken at the GP surgery the week before; results received and filed. No visit needed here.",
        "Samples taken at home by the community nursing team; results received. No visit needed here.");

    /// <summary>A note on an appointment, as the person booking it might add.</summary>
    public static string AppointmentNote(Random random) => Pick(random,
        "Patient prefers morning appointments.",
        "Needs step-free access.",
        "Interpreter booked.",
        "Patient will bring an up-to-date medication list.",
        "Phone reminder requested the day before.",
        "Hard of hearing: text reminder only.",
        "Carer will come with the patient.",
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
        "Visiting clinic cancelled: the consultant was needed at the hospital; patient informed and rebooked.");

    public const string PathwayCancelledAppointmentReason = "Pathway cancelled; patient informed by phone.";

    // Stage notes ----------------------------------------------------------------------------------------------

    private static List<string> Referral(NoteContext c, Random r)
    {
        var parts = new List<string>
        {
            Pick(r, "Referral letter from the GP received and logged.", "GP referral received and checked.", "Referral received; details checked against the GP record."),
            $"Reason: {Pick(r, c.Kind.Indications)}.",
        };
        if (c.Urgent)
            parts.Add("**Urgent referral:** flagged for triage today.");
        else if (r.NextDouble() < 0.4)
            parts.Add(Pick(r, "Routine priority, as referred.", "Recent results attached to the referral.", "Earlier clinic letters asked for from the GP."));
        return parts;
    }

    private static List<string> Triage(NoteContext c, Random r) =>
    [
        c.Urgent ? "Triaged as urgent." : Pick(r, "Triaged as routine.", "Triaged: to be seen soon."),
        Pick(r,
            "Clinic visit first; no tests needed beforehand.",
            "The referral gives enough to book straight into clinic.",
            "Clinic visit first: the history needs taking in person.",
            $"To be seen about {c.Kind.Short} in the next available clinic."),
        r.NextDouble() < 0.5 ? "Nothing more needed from the GP." : "GP asked for the current medication list before the visit.",
    ];

    private static List<string> FirstClinicVisit(NoteContext c, Random r)
    {
        var parts = new List<string> { $"Seen in clinic. Referred with {Pick(r, c.Kind.Indications)}." };
        if (c.Side is { } site)
            parts.Add($"Site: {site}.");
        parts.Add(c.Result.Seen);
        if (r.NextDouble() < 0.3)
            parts.Add(Pick(r, "**Allergies:** none known.", "**Allergy:** penicillin (rash).", "**Takes apixaban:** noted for any biopsy.", "**Allergy:** plasters."));
        parts.Add(Pick(r,
            "What happens next explained; the patient is happy to go ahead.",
            "Options discussed, including doing nothing for now; the patient would like to go ahead.",
            "Questions answered; written information given."));
        parts.Add(Plan(r,
            "Plan:",
            c.Kind.NextStep ?? c.Result.Plan,
            Pick(r, "Leaflet given", "Photographs taken for the record, with consent", "Next visit to be booked"),
            "Letter to the GP"));
        return parts;
    }

    private static List<string> BiopsyOrSample(NoteContext c, Random r)
    {
        string[] risks = c.Kind.Risks.Length == 0 ? ["discomfort"] : c.Kind.Risks.OrderBy(_ => r.Next()).Take(r.Next(2, 4)).ToArray();
        return
        [
            $"{Capitalise(c.Kind.Sample)} taken{(c.Side is { } site ? $" from the {site}" : "")}.",
            c.Kind.SampleNotes.Length > 0 ? Pick(r, c.Kind.SampleNotes) : string.Empty,
            r.NextDouble() < 0.35
                ? Plan(r, "Risks discussed:", risks)
                : $"Risks discussed: {string.Join(", ", risks)}.",
            Pick(r, "Consent confirmed and recorded.", "Consent form signed; copy given."),
            Pick(r, "Specimen labelled at the bedside and sent to the lab.", "Specimen pot labelled and checked with the patient; lab request completed."),
            r.NextDouble() < 0.9
                ? Pick(r, "Uneventful.", "No complications.", "Tolerated well.")
                : "**Minor bleeding** stopped with pressure; observed for an extra 20 minutes.",
        ];
    }

    private static List<string> Histology(NoteContext c, Random r) =>
    [
        Pick(r, "Specimen received and booked in; label and request match.", "Specimen booked in. Details checked against the request."),
        c.Result.Result,
        r.NextDouble() < 0.25 ? "Extra levels cut to show the margin clearly." : string.Empty,
        Pick(r,
            "Report authorised and sent to the requesting clinician.",
            "Reported and authorised; added to the next team meeting list.",
            $"Report authorised {r.Next(4, 11)} working days after it was booked in."),
    ];

    private static List<string> ResultsAndTeamReview(NoteContext c, Random r) =>
    [
        Pick(r, $"Results reviewed at {c.Kind.Team}.", $"Taken to {c.Kind.Team}."),
        c.Result.Result,
        $"Plan: {Lower(c.Result.Plan.TrimEnd('.'))}.",
        Pick(r, "Explained to the patient by phone; letter to follow.", "Results given in clinic, with the nurse present.", "Patient told by phone and sent a copy of the letter."),
    ];

    private static List<string> Treatment(NoteContext c, Random r) =>
    [
        c.Side is { } site ? $"Site: {site}." : string.Empty,
        c.Result.Treatment.Length > 0 ? c.Result.Treatment : "Treatment given as planned.",
        $"Consent confirmed. Local anaesthetic: lidocaine 1% with adrenaline, {r.Next(2, 7)} ml.",
        r.NextDouble() < 0.9
            ? Pick(r, "Uneventful.", "No complications.", "Tolerated well.")
            : "**Minor bleeding** stopped with pressure; observed for an extra 20 minutes.",
        Pick(r,
            $"Sutures out in {Pick(r, "7", "10", "14")} days at the GP surgery.",
            "Dressing to stay dry for 48 hours.",
            "Wound care sheet given."),
        SafetyNet(r),
    ];

    private static List<string> FollowUp(NoteContext c, Random r) =>
    [
        Pick(r, "Reviewed by phone.", "Seen in the nurse-led clinic.", "Follow-up call."),
        c.Kind.FollowUps.Length > 0 ? Pick(r, c.Kind.FollowUps) : "Doing well since the last visit.",
        Pick(r, "Nothing more needed from the centre.", "No further follow-up needed here.", SafetyNet(r)),
    ];

    private static List<string> DischargeLetter(NoteContext c, Random r) =>
    [
        Pick(r, "Discharge letter sent to the GP, with a copy to the patient.", "Letter to the GP dictated, checked and sent; copy to the patient."),
        c.Result.Result,
        $"Plan: {Lower(c.Result.Plan.TrimEnd('.'))}.",
        Pick(r, "GP asked to re-refer if things change.", "Nothing for the GP to do beyond the plan in the letter.", "Patient knows how to get back in touch."),
    ];

    private static List<string> BloodAndUrineTests(NoteContext c, Random r) =>
    [
        $"Bloods taken for {c.Kind.Sample}; urine sample collected.",
        Pick(r, "Left arm, first attempt.", "Right arm, first attempt.", "Second attempt needed; a small bruise expected."),
        r.NextDouble() < 0.6 ? $"BP {Bp(r)}, weight {r.Next(58, 118)} kg." : string.Empty,
        r.NextDouble() < 0.2 ? "**Fasting sample:** nothing eaten since last night." : string.Empty,
        Pick(r, "Samples labelled at the bedside and taken to the lab.", "Labelled with the patient and in the lab within the hour."),
    ];

    private static List<string> LabTests(NoteContext c, Random r) =>
    [
        Pick(r, "Samples received and booked in.", "Samples booked in; labels match the request."),
        c.Result.Result,
        Pick(r,
            "Results authorised and released to the record.",
            "Authorised; requesting clinician told.",
            $"Run on the {Pick(r, "morning", "afternoon")} batch and authorised the same day."),
    ];

    private static List<string> ClinicReview(NoteContext c, Random r)
    {
        var parts = new List<string>
        {
            Pick(r, "Reviewed in clinic with the results.", "Seen in clinic; results gone through together.", "Clinic review."),
            $"BP {Bp(r)}, weight {r.Next(58, 118)} kg, BMI {r.Next(22, 39)}.",
            c.Result.Seen,
        };
        if (r.NextDouble() < 0.3)
            parts.Add(Pick(r, "**Hypo awareness** checked: intact.", "**Driving:** reminded to test before driving.", "**Sick-day rules** gone through."));
        parts.Add(Plan(r, "Plan:", c.Result.Plan, Pick(r, "Letter to the GP", "Copy of the results given to the patient", "Next review in a year")));
        return parts;
    }

    private static List<string> MedicinesReview(NoteContext c, Random r) =>
    [
        Pick(r, "Medicines reviewed with the patient by phone.", "Seen in the pharmacist's clinic.", "Medicines review, with the patient's carer present."),
        c.Kind.Medicines.Length > 0 ? Pick(r, c.Kind.Medicines) : "No changes needed.",
        Pick(r,
            "Taking everything as prescribed.",
            $"Misses the evening dose about {Pick(r, "once", "twice", "three times")} a week: a reminder alarm suggested.",
            "No side effects reported.",
            "No interactions found with the rest of the list."),
        Pick(r, "GP informed of the change.", "Repeat prescription updated via the GP.", "To review at follow-up."),
    ];

    private static List<string> BiomechanicsSession(NoteContext c, Random r) =>
    [
        $"Session {c.Occurrence} of {c.Occurrences}.",
        $"Pain {r.Next(1, 7)}/10; {Pick(r,
            $"single-leg heel raises: {r.Next(6, 26)}",
            $"the ankle bends {r.Next(3, 13)} degrees past neutral",
            $"single-leg balance: {r.Next(5, 31)} seconds")}.",
        Pick(r,
            "Insoles fitted and checked in the patient's own shoes.",
            "Insoles adjusted: more support under the arch.",
            "Loading programme progressed: weight added in a rucksack.",
            "Stretches checked; technique corrected.",
            "Walking checked again on the treadmill: the heel rolls in less."),
        r.NextDouble() < 0.4 ? "**Home programme** reviewed; doing it most days." : "Home programme reviewed.",
    ];

    // Parts ---------------------------------------------------------------------------------------------------

    private static string SafetyNet(Random r) => Pick(r,
        "Safety-netting given: bleeding, spreading redness or a fever → phone the centre.",
        "Advice given on what to look out for and who to call.",
        "Written advice given, with the nurse specialist's phone line.");

    private static string Bp(Random r) => $"{r.Next(106, 166)}/{r.Next(62, 96)}";

    /// <summary>Some lines as a short Markdown list after a heading line; otherwise a sentence.</summary>
    private static string Plan(Random r, string heading, params string[] items)
    {
        var listed = items.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.TrimEnd('.')).ToList();
        return r.NextDouble() < 0.45
            ? heading + "\n" + string.Join("\n", listed.Select(i => $"- {i}"))
            : $"{heading} {string.Join("; ", listed.Select(Lower))}.";
    }

    private static string Compose(IEnumerable<string> parts)
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
