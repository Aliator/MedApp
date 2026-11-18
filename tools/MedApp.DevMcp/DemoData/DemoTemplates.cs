namespace MedApp.DevMcp.DemoData;

/// <summary>One block of a day template: an activity type's name, UK clock times and a note (never about patients).</summary>
public sealed record BlockSpec(string Type, TimeOnly From, TimeOnly To, string Note = "");

/// <summary>A day template as someone set it up: its name, and its blocks.</summary>
public sealed record DayTemplateSpec(string Name, IReadOnlyList<BlockSpec> Blocks);

/// <summary>A week template: its name, and the day template (by name, or none) for Monday to Friday.</summary>
public sealed record WeekTemplateSpec(string Name, IReadOnlyList<string?> MondayToFriday);

/// <summary>
/// One member of staff's own calendar templates (templates are personal: each person has their own, by name), and the
/// week template that's their default.
/// </summary>
public sealed record PersonalTemplates(
    IReadOnlyList<DayTemplateSpec> Days,
    IReadOnlyList<WeekTemplateSpec> Weeks,
    string DefaultWeek);

/// <summary>
/// Everyone's day and week templates, by person: the same shapes for the same work (a clinic day, a minor surgery
/// list, a day of lab reporting), with the differences people have: the visiting consultants' days at the hospital, a
/// part-time week, a late lunch, a Friday list, an admin's management afternoon. They leave the centre's regular
/// meeting times free for the people who go to them (Monday 08:30, Wednesday 16:00, Friday 13:30), as a centre sets
/// its working patterns round them.
/// </summary>
public static class DemoTemplates
{
    public const string ClinicDay = "Clinic day";
    public const string MorningClinic = "Morning clinic";
    public const string AfternoonClinic = "Afternoon clinic";
    public const string MinorSurgeryList = "Minor surgery list";
    public const string FridayList = "Friday list";
    public const string LabReporting = "Lab reporting";
    public const string OutreachClinic = "Outreach clinic";
    public const string TriageSession = "Triage session";
    public const string AdminDay = "Admin day";
    public const string AtTheHospital = "At the hospital";
    public const string DayOff = "Day off";
    public const string OfficeDay = "Office day";
    public const string OfficeEarlyFinish = "Office day (early finish)";
    public const string ClinicAndManagement = "Clinic and management";

    /// <summary>The activity types the centre added to the app's own (name and colour).</summary>
    public static readonly (string Name, string Colour)[] ActivityTypesAdded =
    [
        ("Lab reporting", "yellow"), ("Outreach", "teal"), ("Triage", "orange"), ("Teaching", "pink"), ("Supervision", "red"),
        ("Off site", "purple"), ("Off duty", "gray"),
    ];

    /// <summary>The part-time nurse specialist's two weeks: full time until 20 weeks ago, then mornings.</summary>
    public const string FullTimeWeek = "Full time";
    public const string PartTimeWeek = "Part-time (mornings)";

    /// <summary>The dermatologist moved their list-day lunch later, 10 weeks ago (a dated change to the template).</summary>
    public const string LateLunchUsername = DemoStaff.DermatologistUsername;
    public static readonly BlockSpec ListLunchBefore = new("Lunch", new(12, 45), new(13, 30));
    public static readonly BlockSpec ListLunchAfter = new("Lunch", new(13, 15), new(14, 0), "Later lunch: the list overruns");

    /// <summary>Day templates a kind of visit is best booked on, by name: minor surgery lists for biopsies, say.</summary>
    public static readonly IReadOnlyDictionary<DayKind, string[]> ForDayKind = new Dictionary<DayKind, string[]>
    {
        [DayKind.Clinic] = [ClinicDay, MorningClinic, AfternoonClinic, TriageSession, OutreachClinic, ClinicAndManagement],
        [DayKind.MinorSurgery] = [MinorSurgeryList, FridayList],
    };

    private static BlockSpec B(string type, int fromHour, int fromMinute, int toHour, int toMinute, string note = "") =>
        new(type, new TimeOnly(fromHour, fromMinute), new TimeOnly(toHour, toMinute), note);

    private static DayTemplateSpec Clinic(int lunchHour = 12, int lunchMinute = 30, int lunchMinutes = 45, string lunchNote = "") =>
        new(ClinicDay,
        [
            B("Lunch", lunchHour, lunchMinute, lunchHour + (lunchMinute + lunchMinutes) / 60, (lunchMinute + lunchMinutes) % 60, lunchNote),
            B("Admin time", 17, 0, 18, 0, "Letters and results"),
        ]);

    /// <summary>A list day for whoever operates: preparation, lunch (which the dermatologist later moved), and time at the end.</summary>
    private static DayTemplateSpec List(BlockSpec last) => new(MinorSurgeryList,
    [
        B("Admin time", 8, 0, 8, 30, "List preparation"), ListLunchBefore, last,
    ]);

    /// <summary>The nurses' and the healthcare assistant's list day: setting up, a later lunch, and the specimen log.</summary>
    private static readonly DayTemplateSpec AssistingList = new(MinorSurgeryList,
    [
        B("Admin time", 8, 0, 8, 30, "Setting up the room"), B("Lunch", 13, 0, 13, 30), B("Admin time", 17, 30, 18, 0, "Specimen log and stock"),
    ]);

    private static readonly DayTemplateSpec Friday = new(FridayList,
    [
        B("Admin time", 8, 0, 8, 30, "List preparation"), B("Lunch", 12, 30, 13, 0), B("Admin time", 15, 0, 16, 30, "Histology requests and letters"),
    ]);

    private static readonly DayTemplateSpec Triage = new(TriageSession,
    [
        B("Triage", 9, 0, 10, 30, "Referral triage"), B("Lunch", 12, 30, 13, 15), B("Admin time", 17, 0, 18, 0, "Letters and results"),
    ]);

    private static readonly DayTemplateSpec Admin = new(AdminDay,
    [
        B("Admin time", 9, 0, 12, 0, "Letters, results and audit"), B("Lunch", 12, 0, 12, 45), B("Admin time", 14, 30, 16, 30),
    ]);

    private static DayTemplateSpec Outreach(string note) => new(OutreachClinic,
    [
        B("Outreach", 9, 0, 12, 30, note), B("Lunch", 12, 30, 13, 15), B("Admin time", 17, 0, 18, 0, "Letters and results"),
    ]);

    /// <summary>A day of reporting, with the early afternoon kept free for queries and the Friday team meeting.</summary>
    private static readonly DayTemplateSpec Lab = new(LabReporting,
    [
        B("Lab reporting", 9, 0, 12, 30, "Cut-up and reporting"), B("Lunch", 12, 30, 13, 15),
        B("Lab reporting", 14, 30, 16, 0, "Reporting and sign-out"), B("Admin time", 17, 0, 17, 30, "Quality checks"),
    ]);

    /// <summary>The part-time nurse specialist's morning clinic: off from lunch.</summary>
    private static readonly DayTemplateSpec PartTimeMorning = new(MorningClinic,
    [
        B("Lunch", 12, 30, 13, 0), B("Off duty", 13, 0, 18, 0, "Part-time: mornings only"),
    ]);

    /// <summary>A visiting consultant's morning here, back at the hospital after lunch.</summary>
    private static readonly DayTemplateSpec VisitingMorning = new(MorningClinic,
    [
        B("Lunch", 12, 30, 13, 0), B("Off site", 13, 0, 18, 0, "At the hospital in the afternoon"),
    ]);

    /// <summary>A visiting consultant's afternoon here, after a morning at the hospital.</summary>
    private static readonly DayTemplateSpec VisitingAfternoon = new(AfternoonClinic,
    [
        B("Off site", 8, 0, 12, 30, "At the hospital in the morning"), B("Lunch", 12, 30, 13, 0), B("Admin time", 17, 30, 18, 0, "Letters and results"),
    ]);

    private static readonly DayTemplateSpec Hospital = new(AtTheHospital, [B("Off site", 8, 0, 18, 0, "At the hospital all day")]);

    private static readonly DayTemplateSpec Off = new(DayOff, [B("Off duty", 8, 0, 18, 0, "Not a working day")]);

    private static DayTemplateSpec Office(int lunchHour, int lunchMinute, string lunchNote = "") =>
        new(OfficeDay,
        [
            B("Admin time", 8, 0, 8, 30, "Emails and post"),
            B("Lunch", lunchHour, lunchMinute, lunchHour + (lunchMinute + 45) / 60, (lunchMinute + 45) % 60, lunchNote),
        ]);

    private static readonly DayTemplateSpec OfficeFriday = new(OfficeEarlyFinish,
    [
        B("Admin time", 8, 0, 8, 30, "Emails and post"), B("Lunch", 12, 30, 13, 0),
        B("Off duty", 16, 0, 18, 0, "Compressed hours: early finish on Fridays"),
    ]);

    private static readonly DayTemplateSpec Management = new(ClinicAndManagement,
    [
        B("Lunch", 12, 30, 13, 15), B("Admin time", 14, 0, 15, 30, "Service management"), B("Admin time", 17, 0, 18, 0, "Letters and results"),
    ]);

    /// <summary>Someone's own templates, from what they do.</summary>
    public static PersonalTemplates For(StaffSeed seed) => seed.Kind switch
    {
        // The lead, an admin who also works clinically: a management afternoon on Wednesdays, outreach on Thursdays.
        StaffKind.ClinicalLead => new(
            [Clinic(), Management, Outreach("Diabetes clinic at a GP surgery")],
            [new("Clinical lead week", [ClinicDay, ClinicDay, ClinicAndManagement, OutreachClinic, ClinicDay])],
            "Clinical lead week"),

        // The service manager (the second admin) finishes early on Fridays.
        StaffKind.ServiceManager => new(
            [Office(12, 30), OfficeFriday],
            [new("Office week", [OfficeDay, OfficeDay, OfficeDay, OfficeDay, OfficeEarlyFinish])],
            "Office week"),

        // The dermatologist: two lists a week, a triage session, and an admin day on Fridays.
        StaffKind.Dermatologist => new(
            [Clinic(), List(B("Supervision", 17, 0, 17, 30, "Trainee supervision")), Triage, Admin],
            [new("Dermatology week", [ClinicDay, MinorSurgeryList, TriageSession, MinorSurgeryList, AdminDay])],
            "Dermatology week"),

        // The GP with a special interest in skin operates on Friday mornings, where the dermatologist has an admin day.
        StaffKind.SkinGp => new(
            [Clinic(), List(B("Admin time", 17, 0, 17, 30, "Histology requests")), Triage, Friday],
            [new("Skin clinic week", [ClinicDay, TriageSession, ClinicDay, MinorSurgeryList, FridayList])],
            "Skin clinic week"),

        StaffKind.Leaver => new(
            [Clinic(), List(B("Admin time", 17, 0, 17, 30, "Histology requests")), Triage, Admin],
            [new("Skin clinic week", [ClinicDay, MinorSurgeryList, ClinicDay, TriageSession, AdminDay])],
            "Skin clinic week"),

        // The visiting consultants are here part of the week, and at the hospital the rest.
        StaffKind.Gynaecologist => new(
            [Clinic(), VisitingAfternoon, Hospital],
            [new("Visiting week", [AtTheHospital, ClinicDay, AtTheHospital, AfternoonClinic, AtTheHospital])],
            "Visiting week"),

        StaffKind.Urologist => new(
            [Clinic(), VisitingMorning, Hospital],
            [new("Visiting week", [AtTheHospital, AtTheHospital, ClinicDay, AtTheHospital, MorningClinic])],
            "Visiting week"),

        StaffKind.DiabetesNurse => new(
            [Clinic(), Outreach("Diabetes clinic at a GP surgery"), PartTimeMorning, Off],
            [
                new(FullTimeWeek, [ClinicDay, ClinicDay, OutreachClinic, ClinicDay, ClinicDay]),
                new(PartTimeWeek, [MorningClinic, MorningClinic, MorningClinic, MorningClinic, DayOff]),
            ],
            PartTimeWeek),

        StaffKind.BiomedicalScientist => new(
            [Lab],
            [new("Lab week", [LabReporting, LabReporting, LabReporting, LabReporting, LabReporting])],
            "Lab week"),

        StaffKind.Podiatrist => new(
            [Clinic(13, 0, 30), Outreach("Foot clinic at a community hall")],
            [new("Biomechanics week", [ClinicDay, ClinicDay, ClinicDay, OutreachClinic, ClinicDay])],
            "Biomechanics week"),

        StaffKind.Pharmacist => new(
            [Clinic(), Admin],
            [new("Pharmacy week", [ClinicDay, ClinicDay, ClinicDay, ClinicDay, AdminDay])],
            "Pharmacy week"),

        // The booking coordinator lunches late, covering the desk while the others are at lunch.
        StaffKind.BookingCoordinator => new(
            [Office(13, 30, "Late lunch: covering the desk until 13:30")],
            [new("Office week", [OfficeDay, OfficeDay, OfficeDay, OfficeDay, OfficeDay])],
            "Office week"),

        // The skin cancer nurse specialist: assisting the Tuesday list, and an outreach clinic on Thursdays.
        StaffKind.SkinCancerNurse => new(
            [Clinic(), AssistingList, Outreach("Skin check clinic at a GP surgery")],
            [new("Nursing week", [ClinicDay, MinorSurgeryList, ClinicDay, OutreachClinic, ClinicDay])],
            "Nursing week"),

        // The healthcare assistant and the new starter: assisting the Thursday list.
        _ => new(
            [Clinic(), AssistingList],
            [new("Nursing week", [ClinicDay, ClinicDay, ClinicDay, MinorSurgeryList, ClinicDay])],
            "Nursing week"),
    };
}
