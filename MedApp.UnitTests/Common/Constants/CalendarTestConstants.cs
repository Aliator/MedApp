using MedApp.Application.Calendar.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Calendar;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>
/// Synthetic data for calendar and appointment tests. "Today" is Monday 16 November 2026, when UK time is GMT (UTC+0);
/// the clocks went back on Sunday 25 October 2026, so Friday 23 October was BST (UTC+1).
/// </summary>
public static class CalendarTestConstants
{
    /// <summary>"Now": 09:00 on Monday 16 November 2026, UTC and UK time alike.</summary>
    public static readonly DateTime Now = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);

    public static readonly DateOnly Today = new(2026, 11, 16);

    /// <summary>The Monday a week after <see cref="Today"/>, and that week's Friday, Saturday and Sunday.</summary>
    public static readonly DateOnly NextMonday = new(2026, 11, 23);
    public static readonly DateOnly NextFriday = new(2026, 11, 27);
    public static readonly DateOnly NextSaturday = new(2026, 11, 28);
    public static readonly DateOnly NextSunday = new(2026, 11, 29);

    /// <summary>The Friday before the clocks went back (BST) and the Monday after (GMT).</summary>
    public static readonly DateOnly FridayBeforeClockChange = new(2026, 10, 23);
    public static readonly DateOnly MondayAfterClockChange = new(2026, 10, 26);

    /// <summary>A summer weekday (BST).</summary>
    public static readonly DateOnly SummerMonday = new(2026, 6, 15);

    /// <summary>Long before any test date, for rows that have always applied.</summary>
    public static readonly DateOnly LongAgo = new(2026, 1, 1);

    public static readonly Guid StaffUserId = Guid.Parse("0000000c-0000-0000-0000-000000000001");
    public static readonly Guid OtherStaffUserId = Guid.Parse("0000000c-0000-0000-0000-000000000002");
    public const string StaffUsername = "calendar.staff";
    public const string OtherStaffUsername = "calendar.other";

    public static readonly Guid DayTemplateId = Guid.Parse("0000000c-0000-0000-0000-000000000010");
    public static readonly Guid OtherDayTemplateId = Guid.Parse("0000000c-0000-0000-0000-000000000011");
    public static readonly Guid WeekTemplateId = Guid.Parse("0000000c-0000-0000-0000-000000000020");
    public static readonly Guid OtherWeekTemplateId = Guid.Parse("0000000c-0000-0000-0000-000000000021");

    public const string DayTemplateName = "Clinic day";
    public const string OtherDayTemplateName = "Theatre day";

    /// <summary>A patient name that must never appear in a clash message.</summary>
    public const string PatientFirstName = "Synthetic";
    public const string PatientLastName = "Patient";

    public static readonly CalendarActivityType Lunch = new()
    {
        Id = Guid.Parse("22222222-0000-0000-0000-000000000001"),
        Name = "Lunch",
        Colour = "green"
    };

    public static readonly CalendarActivityType Training = new()
    {
        Id = Guid.Parse("22222222-0000-0000-0000-000000000003"),
        Name = "Training",
        Colour = "purple"
    };

    /// <summary>A UTC moment from UK clock time on a UK date.</summary>
    public static DateTime At(DateOnly date, int hour, int minute = 0) => UkTime.ToUtc(date, new TimeOnly(hour, minute));

    /// <summary>A day template block, valid from <paramref name="validFrom"/> (long ago by default) to <paramref name="validTo"/>.</summary>
    public static DayTemplateBlock CreateTemplateBlock(
        int startHour,
        int endHour,
        CalendarActivityType? activityType = null,
        Guid? dayTemplateId = null,
        DateOnly? validFrom = null,
        DateOnly? validTo = null,
        int startMinute = 0,
        int endMinute = 0,
        string note = "")
    {
        activityType ??= Lunch;
        return new DayTemplateBlock
        {
            Id = Guid.NewGuid(),
            DayTemplateId = dayTemplateId ?? DayTemplateId,
            ActivityTypeId = activityType.Id,
            ActivityType = activityType,
            StartTime = new TimeOnly(startHour, startMinute),
            EndTime = new TimeOnly(endHour, endMinute),
            Note = note,
            ValidFrom = validFrom ?? LongAgo,
            ValidTo = validTo
        };
    }

    /// <summary>A week template's day: the day template it uses on a weekday.</summary>
    public static WeekTemplateDay CreateWeekDay(
        DayOfWeek dayOfWeek,
        Guid? dayTemplateId = null,
        Guid? weekTemplateId = null,
        DateOnly? validFrom = null,
        DateOnly? validTo = null) => new()
    {
        Id = Guid.NewGuid(),
        WeekTemplateId = weekTemplateId ?? WeekTemplateId,
        DayOfWeek = dayOfWeek,
        DayTemplateId = dayTemplateId ?? DayTemplateId,
        ValidFrom = validFrom ?? LongAgo,
        ValidTo = validTo
    };

    /// <summary>Monday to Friday each using <paramref name="dayTemplateId"/> (the clinic day by default).</summary>
    public static List<WeekTemplateDay> CreateWorkingWeek(Guid? dayTemplateId = null, Guid? weekTemplateId = null) =>
        CalendarSetupMapper.Weekdays.Select(d => CreateWeekDay(d, dayTemplateId, weekTemplateId)).ToList();

    /// <summary>A person's default week template.</summary>
    public static StaffWeekTemplate CreateStaffWeek(
        Guid? weekTemplateId = null,
        Guid? staffUserId = null,
        DateOnly? validFrom = null,
        DateOnly? validTo = null) => new()
    {
        Id = Guid.NewGuid(),
        StaffUserId = staffUserId ?? StaffUserId,
        WeekTemplateId = weekTemplateId ?? WeekTemplateId,
        ValidFrom = validFrom ?? LongAgo,
        ValidTo = validTo
    };

    /// <summary>A stored block on <see cref="StaffUserId"/>'s calendar.</summary>
    public static CalendarBlock CreateBlock(DateTime startUtc, DateTime endUtc, CalendarActivityType? activityType = null)
    {
        activityType ??= Training;
        return new CalendarBlock
        {
            Id = Guid.NewGuid(),
            StaffUserId = StaffUserId,
            ActivityTypeId = activityType.Id,
            ActivityType = activityType,
            StartUtc = startUtc,
            EndUtc = endUtc,
            CreatedAtUtc = Now,
            CreatedByUserId = StaffUserId,
            LastUpdated = Now
        };
    }

    /// <summary>An appointment with <see cref="StaffUserId"/> (none while Requested), for a synthetic patient.</summary>
    public static Appointment CreateAppointment(
        AppointmentStatus status = AppointmentStatus.Booked,
        DateTime? startUtc = null,
        int minutes = 30,
        Guid? stageId = null)
    {
        var booked = status != AppointmentStatus.Requested;
        var start = startUtc ?? At(NextMonday, 10);
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.FirstName = PatientFirstName;
        patient.LastName = PatientLastName;

        return new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Patient = patient,
            StageId = stageId,
            StaffUserId = booked ? StaffUserId : null,
            StartUtc = booked ? start : null,
            EndUtc = booked ? start.AddMinutes(minutes) : null,
            Status = status,
            CreatedAtUtc = Now.AddDays(-1),
            CreatedByUserId = StaffUserId,
            BookedAtUtc = booked ? Now.AddDays(-1) : null,
            BookedByUserId = booked ? StaffUserId : null,
            LastUpdated = Now.AddDays(-1)
        };
    }

    /// <summary>
    /// <see cref="StaffUserId"/>'s calendar from <paramref name="from"/> to <paramref name="to"/>: by default their
    /// default week is the working week, each weekday the clinic day, with the template blocks given.
    /// </summary>
    public static CalendarData CreateData(
        DateOnly from,
        DateOnly? to = null,
        IEnumerable<DayTemplateBlock>? templateBlocks = null,
        IEnumerable<StaffWeekTemplate>? staffWeeks = null,
        IEnumerable<WeekTemplateDay>? weekDays = null,
        IEnumerable<CalendarDayOverride>? overrides = null,
        IEnumerable<CalendarBlockException>? exceptions = null,
        IEnumerable<CalendarBlock>? blocks = null,
        IEnumerable<Appointment>? appointments = null) =>
        new(
            StaffUserId,
            from,
            to ?? from,
            (staffWeeks ?? [CreateStaffWeek()]).ToList(),
            (weekDays ?? CreateWorkingWeek()).ToList(),
            (templateBlocks ?? []).ToList(),
            (overrides ?? []).ToList(),
            (exceptions ?? []).ToList(),
            (blocks ?? []).ToList(),
            (appointments ?? []).ToList(),
            new Dictionary<Guid, string> { [DayTemplateId] = DayTemplateName, [OtherDayTemplateId] = OtherDayTemplateName });

    /// <summary>A day override for <see cref="StaffUserId"/>: the date uses <paramref name="dayTemplateId"/> (or none).</summary>
    public static CalendarDayOverride CreateOverride(DateOnly date, Guid? dayTemplateId) => new()
    {
        Id = Guid.NewGuid(),
        StaffUserId = StaffUserId,
        Date = date,
        DayTemplateId = dayTemplateId
    };

    /// <summary>An exception leaving a template block off one date of <see cref="StaffUserId"/>'s calendar.</summary>
    public static CalendarBlockException CreateException(DateOnly date, DayTemplateBlock block) => new()
    {
        Id = Guid.NewGuid(),
        StaffUserId = StaffUserId,
        Date = date,
        DayTemplateBlockId = block.Id
    };
}
