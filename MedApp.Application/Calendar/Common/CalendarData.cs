using MedApp.Domain.Appointments;
using MedApp.Domain.Calendar;
using MedApp.Domain.Clinics;

namespace MedApp.Application.Calendar.Common;

/// <summary>
/// Everything that puts something on one member of staff's calendar over a range of UK dates, as the repository loads
/// it (<c>ICalendarRepository.GetCalendarDataAsync</c>), for <see cref="CalendarProjection"/>,
/// <see cref="CalendarClashes"/> and <see cref="Availability"/>. Dated rows are those valid on any date in the range.
/// </summary>
/// <param name="StaffWeekTemplates">The person's default week template rows, each with its week template.</param>
/// <param name="WeekTemplateDays">The days of those week templates.</param>
/// <param name="DayTemplateBlocks">
/// The blocks of every day template those days or the range's overrides use, each with its activity type.
/// </param>
/// <param name="DayOverrides">Dates using a day template of their own (or none).</param>
/// <param name="BlockExceptions">Template blocks left off a date (changed or removed that day).</param>
/// <param name="Blocks">Stored blocks in the range, each with its activity type.</param>
/// <param name="Appointments">
/// The person's Booked, Attended and Did not attend appointments in the range, with their patient, pathway and stage.
/// </param>
/// <param name="DayTemplateNames">The names of the day templates the days and overrides use, by ID.</param>
public sealed record CalendarData(
    Guid StaffUserId,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<StaffWeekTemplate> StaffWeekTemplates,
    IReadOnlyList<WeekTemplateDay> WeekTemplateDays,
    IReadOnlyList<DayTemplateBlock> DayTemplateBlocks,
    IReadOnlyList<CalendarDayOverride> DayOverrides,
    IReadOnlyList<CalendarBlockException> BlockExceptions,
    IReadOnlyList<CalendarBlock> Blocks,
    IReadOnlyList<Appointment> Appointments,
    IReadOnlyDictionary<Guid, string> DayTemplateNames)
{
    /// <summary>The meetings the person is going to in the range, each with its attendees.</summary>
    public IReadOnlyList<Meeting> Meetings { get; init; } = [];

    /// <summary>
    /// The person's Planned clinic sessions in the range, each with its room and team: busy time for blocks,
    /// meetings and appointments outside them, and where appointments into their slots go (centre plan decision 123).
    /// </summary>
    public IReadOnlyList<ClinicSession> Sessions { get; init; } = [];

    /// <summary>The days in the range the clinic is closed: no templates apply on them, and nothing new can go on them.</summary>
    public ClosedDaySet ClosedDays { get; init; } = ClosedDaySet.None;

    /// <summary>
    /// The person's working hours over the range, with their sites' hours (none: the default, Monday to Friday, 08:00
    /// to 18:00). With <see cref="ClosedDays"/>, they decide each date's <see cref="WorkingTimeOn"/>.
    /// </summary>
    public WorkingTimeSet Hours { get; init; } = WorkingTimeSet.None;

    /// <summary>The person's working day on a date in the range (<see cref="WorkingTime"/>).</summary>
    public WorkingTime WorkingTimeOn(DateOnly date) => Hours.For(StaffUserId, date, ClosedDays);

    /// <summary>Nothing on the calendar: no templates, blocks, appointments or meetings.</summary>
    public static CalendarData Empty(Guid staffUserId, DateOnly from, DateOnly to) =>
        new(staffUserId, from, to, [], [], [], [], [], [], [], new Dictionary<Guid, string>());
}
