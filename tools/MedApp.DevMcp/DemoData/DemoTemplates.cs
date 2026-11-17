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
/// Everyone's day and week templates, by person: the same shapes for the same work (a clinic day, a theatre day), with
/// the differences people have: a part-time week, a late lunch, a different Friday, an admin's management afternoon.
/// They leave the clinic's regular meeting times free (Monday 08:30, Wednesday 16:00, Friday 13:30), as a clinic sets
/// its working patterns round them.
/// </summary>
public static class DemoTemplates
{
    public const string ClinicDay = "Clinic day";
    public const string TheatreDay = "Theatre day";
    public const string EndoscopyList = "Endoscopy list";
    public const string AdminDay = "Admin day";
    public const string FridayList = "Friday list";
    public const string HalfDayMorning = "Half day (morning)";
    public const string DayOff = "Day off";
    public const string OfficeDay = "Office day";
    public const string OfficeEarlyFinish = "Office day (early finish)";
    public const string ClinicAndManagement = "Clinic and management";

    /// <summary>The part-time nurse's two weeks: full time until 20 weeks ago, then mornings.</summary>
    public const string FullTimeWeek = "Full time";
    public const string PartTimeWeek = "Part-time (mornings)";

    /// <summary>The endoscopist moved their list-day lunch later, 10 weeks ago (a dated change to the template).</summary>
    public const string LateLunchUsername = DemoStaff.NoAssignmentNotificationsUsername;
    public static readonly BlockSpec EndoscopyLunchBefore = new("Lunch", new(12, 45), new(13, 30));
    public static readonly BlockSpec EndoscopyLunchAfter = new("Lunch", new(13, 15), new(14, 0), "Later lunch: the list overruns");

    /// <summary>Day templates a kind of visit is best booked on, by name: theatre days for operations, say.</summary>
    public static readonly IReadOnlyDictionary<DayKind, string[]> ForDayKind = new Dictionary<DayKind, string[]>
    {
        [DayKind.Clinic] = [ClinicDay, HalfDayMorning, ClinicAndManagement],
        [DayKind.Theatre] = [TheatreDay, FridayList],
        [DayKind.Endoscopy] = [EndoscopyList],
    };

    private static BlockSpec B(string type, int fromHour, int fromMinute, int toHour, int toMinute, string note = "") =>
        new(type, new TimeOnly(fromHour, fromMinute), new TimeOnly(toHour, toMinute), note);

    private static DayTemplateSpec Clinic(int lunchHour = 12, int lunchMinute = 30, int lunchMinutes = 45, string lunchNote = "") =>
        new(ClinicDay,
        [
            B("Lunch", lunchHour, lunchMinute, lunchHour + (lunchMinute + lunchMinutes) / 60, (lunchMinute + lunchMinutes) % 60, lunchNote),
            B("Admin time", 17, 0, 18, 0, "Letters and results"),
        ]);

    private static readonly DayTemplateSpec Theatre = new(TheatreDay,
    [
        B("Ward round", 8, 0, 8, 30), B("Lunch", 13, 0, 13, 30), B("Admin time", 17, 30, 18, 0, "Operation notes"),
    ]);

    private static readonly DayTemplateSpec Endoscopy = new(EndoscopyList,
    [
        B("Admin time", 8, 0, 8, 30, "List preparation"), EndoscopyLunchBefore, B("Supervision", 17, 0, 17, 30, "Trainee supervision"),
    ]);

    private static readonly DayTemplateSpec Admin = new(AdminDay,
    [
        B("Admin time", 9, 0, 12, 0, "Letters, results and audit"), B("Lunch", 12, 0, 12, 45), B("Admin time", 14, 30, 16, 30),
    ]);

    private static readonly DayTemplateSpec Friday = new(FridayList,
    [
        B("Ward round", 8, 0, 8, 30), B("Lunch", 12, 30, 13, 0), B("Admin time", 15, 0, 16, 30, "Operation notes"),
    ]);

    private static readonly DayTemplateSpec HalfDay = new(HalfDayMorning,
    [
        B("Lunch", 12, 30, 13, 0), B("Off duty", 13, 0, 18, 0, "Part-time: mornings only"),
    ]);

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

    /// <summary>Someone's own templates, from what they do (and, for a few, who they are).</summary>
    public static PersonalTemplates For(StaffSeed seed) => seed.Kind switch
    {
        // The lead, an admin who also works clinically: a management afternoon on Wednesdays.
        StaffKind.ClinicalLead => new(
            [Clinic(), Theatre, Management],
            [new("Clinical lead week", [ClinicDay, ClinicDay, ClinicAndManagement, TheatreDay, ClinicDay])],
            "Clinical lead week"),

        // The service manager (the second admin) finishes early on Fridays.
        StaffKind.ServiceManager => new(
            [Office(12, 30), OfficeFriday],
            [new("Office week", [OfficeDay, OfficeDay, OfficeDay, OfficeDay, OfficeEarlyFinish])],
            "Office week"),

        // One surgeon operates on Friday mornings instead of an admin day.
        StaffKind.Surgeon when seed.Username == "d.ashworth" => new(
            [Clinic(), Theatre, Friday],
            [new("Surgeon week", [ClinicDay, TheatreDay, ClinicDay, TheatreDay, FridayList])],
            "Surgeon week"),

        StaffKind.Surgeon or StaffKind.Leaver => new(
            [Clinic(), Theatre, Admin],
            [new("Surgeon week", [ClinicDay, TheatreDay, ClinicDay, TheatreDay, AdminDay])],
            "Surgeon week"),

        StaffKind.Endoscopist => new(
            [Endoscopy, Clinic(), Admin],
            [new("Endoscopy week", [EndoscopyList, ClinicDay, EndoscopyList, ClinicDay, AdminDay])],
            "Endoscopy week"),

        StaffKind.PartTimeNurse => new(
            [Clinic(), Theatre, HalfDay, Off],
            [
                new(FullTimeWeek, [ClinicDay, ClinicDay, TheatreDay, ClinicDay, ClinicDay]),
                new(PartTimeWeek, [HalfDayMorning, HalfDayMorning, HalfDayMorning, HalfDayMorning, DayOff]),
            ],
            PartTimeWeek),

        StaffKind.Physiotherapist => new(
            [Clinic(13, 0, 30)],
            [new("Physiotherapy week", [ClinicDay, ClinicDay, ClinicDay, ClinicDay, ClinicDay])],
            "Physiotherapy week"),

        // The booking coordinator lunches late, covering the desk while the others are at lunch.
        StaffKind.BookingCoordinator => new(
            [Office(13, 30, "Late lunch: covering the desk until 13:30")],
            [new("Office week", [OfficeDay, OfficeDay, OfficeDay, OfficeDay, OfficeDay])],
            "Office week"),

        // The nurses, the healthcare assistant and the new starter.
        _ => new(
            [Clinic(), Theatre],
            [new("Nursing week", [ClinicDay, ClinicDay, TheatreDay, ClinicDay, ClinicDay])],
            "Nursing week"),
    };
}
