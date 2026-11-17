using System.Globalization;
using MedApp.Application.Appointments.Common;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Domain.Appointments;
using MedApp.Domain.Calendar;

namespace MedApp.Application.Calendar.Common;

/// <summary>Members of staff's calendars as the API returns them.</summary>
public static class CalendarMapper
{
    /// <summary>
    /// A calendar: its days and template blocks worked out from <paramref name="data"/> (<see cref="CalendarProjection"/>),
    /// its stored blocks, its appointments and its meetings, by start time. Blocks can be changed when <paramref name="canEdit"/> and
    /// they aren't on a past day.
    /// </summary>
    /// <param name="defaultWeek">The person's default week template row valid today, with its week template.</param>
    /// <param name="usernames">The usernames the appointments and meetings name (<see cref="UserIds"/>).</param>
    /// <param name="isAdmin">Whether the signed-in user is an admin, who can change meetings.</param>
    public static CalendarResponse ToResponse(
        CalendarData data,
        string staffUsername,
        StaffWeekTemplate? defaultWeek,
        bool canEdit,
        DateOnly today,
        IReadOnlyDictionary<Guid, string> usernames,
        DateTime nowUtc,
        bool isAdmin = false)
    {
        var appointments = data.Appointments
            .Where(a => AppointmentWorkflow.IsOnCalendar(a.Status) && a.StartUtc is not null && a.EndUtc is not null)
            .OrderBy(a => a.StartUtc)
            .ThenBy(a => a.Id)
            .ToList();

        var stored = data.Blocks.Select(b => ToResponse(b, canEdit, today, OverlapsAny(appointments, b.StartUtc, b.EndUtc)));
        var projected = CalendarProjection.TemplateBlocks(data)
            .Select(p => ToResponse(p, data.StaffUserId, canEdit, today, OverlapsAny(appointments, p.StartUtc, p.EndUtc)));

        return new CalendarResponse
        {
            StaffUserId = data.StaffUserId,
            StaffUsername = staffUsername,
            From = data.From,
            To = data.To,
            WorkingDayStart = WorkingHours.Start.ToString("HH:mm", CultureInfo.InvariantCulture),
            WorkingDayEnd = WorkingHours.End.ToString("HH:mm", CultureInfo.InvariantCulture),
            SlotMinutes = WorkingHours.SlotMinutes,
            CanEdit = canEdit,
            DefaultWeekTemplateId = defaultWeek?.WeekTemplateId,
            DefaultWeekTemplateName = defaultWeek?.WeekTemplate?.Name,
            Days = CalendarProjection.Days(data, today).Select(ToResponse).ToList(),
            Blocks = stored.Concat(projected).OrderBy(b => b.StartUtc).ThenBy(b => b.EndUtc).ToList(),
            Appointments = appointments.Select(a => AppointmentMapper.ToResponse(a, usernames, nowUtc)).ToList(),
            Meetings = data.Meetings
                .OrderBy(m => m.StartUtc)
                .ThenBy(m => m.Id)
                .Select(m => MeetingMapper.ToResponse(m, usernames, isAdmin, today))
                .ToList()
        };
    }

    /// <summary>Every user a calendar's response names: its appointments' (<see cref="AppointmentMapper.UserIds"/>) and its meetings' people.</summary>
    public static IEnumerable<Guid> UserIds(CalendarData data) =>
        data.Appointments.SelectMany(AppointmentMapper.UserIds)
            .Concat(data.Meetings.SelectMany(MeetingMapper.UserIds))
            .Distinct();

    public static CalendarDayResponse ToResponse(CalendarDay day) => new()
    {
        Date = day.Date,
        IsWorkingDay = day.IsWorkingDay,
        IsPast = day.IsPast,
        IsToday = day.IsToday,
        WorkStartUtc = day.WorkStartUtc,
        WorkEndUtc = day.WorkEndUtc,
        DayTemplateId = day.DayTemplateId,
        DayTemplateName = day.DayTemplateName,
        IsDayTemplateOverridden = day.IsDayTemplateOverridden
    };

    /// <summary>A stored block, with its activity type (<paramref name="activityType"/>, or the block's own).</summary>
    public static CalendarBlockResponse ToResponse(
        CalendarBlock block,
        bool canEditCalendar,
        DateOnly today,
        bool overlapsAppointment,
        CalendarActivityType? activityType = null)
    {
        var date = UkTime.DateOf(block.StartUtc);
        activityType ??= block.ActivityType;

        return new CalendarBlockResponse
        {
            BlockId = block.Id,
            TemplateBlockId = null,
            IsFromTemplate = false,
            StaffUserId = block.StaffUserId,
            Date = date,
            StartUtc = block.StartUtc,
            EndUtc = block.EndUtc,
            ActivityTypeId = block.ActivityTypeId,
            ActivityName = activityType?.Name ?? string.Empty,
            Colour = activityType?.Colour ?? string.Empty,
            Note = block.Note,
            CanEdit = canEditCalendar && date >= today,
            OverlapsAppointment = overlapsAppointment
        };
    }

    /// <summary>A day template's block as it falls on one date of a person's calendar.</summary>
    public static CalendarBlockResponse ToResponse(
        ProjectedBlock block,
        Guid staffUserId,
        bool canEditCalendar,
        DateOnly today,
        bool overlapsAppointment) => new()
    {
        BlockId = null,
        TemplateBlockId = block.Source.Id,
        IsFromTemplate = true,
        StaffUserId = staffUserId,
        Date = block.Date,
        StartUtc = block.StartUtc,
        EndUtc = block.EndUtc,
        ActivityTypeId = block.Source.ActivityTypeId,
        ActivityName = block.Source.ActivityType?.Name ?? string.Empty,
        Colour = block.Source.ActivityType?.Colour ?? string.Empty,
        Note = block.Source.Note,
        CanEdit = canEditCalendar && block.Date >= today,
        OverlapsAppointment = overlapsAppointment
    };

    /// <summary>A member of staff, with their default week template row valid now (and its week template), if any.</summary>
    public static StaffMemberResponse ToResponse((Guid UserId, string Username) staff, StaffWeekTemplate? defaultWeek) => new()
    {
        UserId = staff.UserId,
        Username = staff.Username,
        WeekTemplateId = defaultWeek?.WeekTemplateId,
        WeekTemplateName = defaultWeek?.WeekTemplate?.Name
    };

    /// <summary>A person's default week template row valid now (with its week template), or none.</summary>
    public static StaffWeekTemplateResponse ToResponse(Guid staffUserId, StaffWeekTemplate? defaultWeek) => new()
    {
        StaffUserId = staffUserId,
        WeekTemplateId = defaultWeek?.WeekTemplateId,
        WeekTemplateName = defaultWeek?.WeekTemplate?.Name,
        Since = defaultWeek?.ValidFrom
    };

    private static bool OverlapsAny(IEnumerable<Appointment> appointments, DateTime startUtc, DateTime endUtc) =>
        appointments.Any(a => CalendarClashes.Overlaps(startUtc, endUtc, a.StartUtc!.Value, a.EndUtc!.Value));
}
