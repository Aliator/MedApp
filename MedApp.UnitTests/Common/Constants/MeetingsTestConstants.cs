using MedApp.Application.Calendar.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Calendar;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>
/// Synthetic data for meeting, team view and suggested time tests. Dates, people and actors come from
/// <see cref="CalendarTestConstants"/> ("now" is 09:00 on Monday 16 November 2026, UK time).
/// </summary>
public static class MeetingsTestConstants
{
    public static readonly Guid MeetingId = Guid.Parse("0000000d-0000-0000-0000-000000000001");
    public static readonly Guid OtherMeetingId = Guid.Parse("0000000d-0000-0000-0000-000000000002");

    public const string Title = "MDT";
    public const string OtherTitle = "Team huddle";
    public const string Notes = "Synthetic meeting notes";

    /// <summary>An all-day absence, for a person on leave.</summary>
    public static readonly CalendarActivityType Leave = new()
    {
        Id = Guid.Parse("22222222-0000-0000-0000-00000000000d"),
        Name = "Annual leave",
        Colour = "gray"
    };

    /// <summary>The usernames of <see cref="CalendarTestConstants.Staff"/>, by ID, as the identity service gives them.</summary>
    public static readonly IReadOnlyDictionary<Guid, string> Usernames = Staff.ToDictionary(s => s.UserId, s => s.Username);

    /// <summary>
    /// A meeting <see cref="CalendarTestConstants.AdminUserId"/> arranged a day ago, for <paramref name="attendeeUserIds"/>
    /// (<see cref="CalendarTestConstants.StaffUserId"/> and <see cref="CalendarTestConstants.OtherStaffUserId"/> by default).
    /// </summary>
    public static Meeting NewMeeting(
        DateTime startUtc,
        DateTime endUtc,
        IEnumerable<Guid>? attendeeUserIds = null,
        Guid? id = null,
        string title = Title)
    {
        var meetingId = id ?? MeetingId;
        return new Meeting
        {
            Id = meetingId,
            Title = title,
            Notes = Notes,
            StartUtc = startUtc,
            EndUtc = endUtc,
            OrganiserUserId = AdminUserId,
            CreatedAtUtc = Now.AddDays(-1),
            LastUpdated = Now.AddDays(-1),
            Attendees = (attendeeUserIds ?? [StaffUserId, OtherStaffUserId])
                .Select(staff => new MeetingAttendee { MeetingId = meetingId, StaffUserId = staff })
                .ToList()
        };
    }

    /// <summary>A separate copy of a meeting, as a second read from the database would give.</summary>
    public static Meeting Copy(Meeting meeting) => new()
    {
        Id = meeting.Id,
        Title = meeting.Title,
        Notes = meeting.Notes,
        StartUtc = meeting.StartUtc,
        EndUtc = meeting.EndUtc,
        OrganiserUserId = meeting.OrganiserUserId,
        CreatedAtUtc = meeting.CreatedAtUtc,
        LastUpdated = meeting.LastUpdated,
        Attendees = meeting.Attendees
            .Select(a => new MeetingAttendee { MeetingId = a.MeetingId, StaffUserId = a.StaffUserId, Reply = a.Reply, RepliedAtUtc = a.RepliedAtUtc })
            .ToList()
    };

    /// <summary>
    /// A person's calendar from <paramref name="from"/> to <paramref name="to"/>: the working week, each weekday the
    /// clinic day with the template blocks given, plus stored blocks, appointments and meetings.
    /// </summary>
    public static CalendarData CalendarOf(
        Guid staffUserId,
        DateOnly from,
        DateOnly? to = null,
        IEnumerable<DayTemplateBlock>? templateBlocks = null,
        IEnumerable<CalendarBlock>? blocks = null,
        IEnumerable<Appointment>? appointments = null,
        IEnumerable<Meeting>? meetings = null) =>
        CreateData(
            from,
            to,
            templateBlocks: templateBlocks,
            staffWeeks: [CreateStaffWeek(staffUserId: staffUserId)],
            blocks: blocks,
            appointments: appointments) with
        {
            StaffUserId = staffUserId,
            Meetings = (meetings ?? []).ToList()
        };

    /// <summary>A time range on a UK date, from one UK clock time to another.</summary>
    public static TimeRange Range(DateOnly date, int startHour, int startMinute, int endHour, int endMinute) =>
        new(At(date, startHour, startMinute), At(date, endHour, endMinute));
}
