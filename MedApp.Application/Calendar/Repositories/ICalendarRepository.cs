using MedApp.Application.Calendar.Common;
using MedApp.Domain.Calendar;

namespace MedApp.Application.Calendar.Repositories;

/// <summary>
/// Members of staff's calendars: their default week, day overrides, blocks and the exceptions to template blocks.
/// Changes that put something on a calendar run inside <see cref="ICalendarLock"/>, which these share.
/// </summary>
public interface ICalendarRepository
{
    /// <summary>Everything on one person's calendar from <paramref name="from"/> to <paramref name="to"/> (UK dates), read-only.</summary>
    Task<CalendarData> GetCalendarDataAsync(Guid staffUserId, DateOnly from, DateOnly to, CancellationToken ct);

    /// <summary>
    /// Everything on each of these people's calendars from <paramref name="from"/> to <paramref name="to"/>, read-only,
    /// loaded together: one <see cref="CalendarData"/> per person, in the order given (each once).
    /// </summary>
    Task<IReadOnlyList<CalendarData>> GetCalendarsAsync(
        IReadOnlyCollection<Guid> staffUserIds, DateOnly from, DateOnly to, CancellationToken ct);

    /// <summary>
    /// What each member of staff has on a UK date: Booked, Attended and Did not attend appointments, stored blocks and
    /// meetings, counted. Only people with something on it.
    /// </summary>
    Task<IReadOnlyList<StaffBookings>> GetBookingsOnAsync(DateOnly date, CancellationToken ct);

    /// <summary>Everyone's default week template row valid on a date, each with its week template.</summary>
    Task<IReadOnlyList<StaffWeekTemplate>> GetStaffWeekTemplatesOnAsync(DateOnly date, CancellationToken ct);

    /// <summary>A person's default week template row valid on a date, with its week template, read-only.</summary>
    Task<StaffWeekTemplate?> GetStaffWeekTemplateOnAsync(Guid staffUserId, DateOnly date, CancellationToken ct);

    /// <summary>A person's default week template rows valid on <paramref name="today"/> or later, tracked for <see cref="SaveStaffWeekTemplatesAsync"/>.</summary>
    Task<IReadOnlyList<StaffWeekTemplate>> GetStaffWeekTemplatesForUpdateAsync(Guid staffUserId, DateOnly today, CancellationToken ct);

    /// <summary>Applies a change to a person's default week template rows (see <see cref="TemplateHistory"/>).</summary>
    Task SaveStaffWeekTemplatesAsync(TemplateHistoryChange<StaffWeekTemplate> change, CancellationToken ct);

    /// <summary>A stored block with its activity type, read-only.</summary>
    Task<CalendarBlock?> GetBlockAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// A stored block, tracked so <see cref="SaveAsync"/> saves changes to it. Its activity type isn't loaded: change
    /// <see cref="CalendarBlock.ActivityTypeId"/>.
    /// </summary>
    Task<CalendarBlock?> GetBlockForUpdateAsync(Guid id, CancellationToken ct);

    Task AddBlockAsync(CalendarBlock block, CancellationToken ct);

    /// <summary>Deletes a block loaded with <see cref="GetBlockForUpdateAsync"/>.</summary>
    Task DeleteBlockAsync(CalendarBlock block, CancellationToken ct);

    /// <summary>Leaves a template block off one date, with the block that takes its place that day, if any, in one save.</summary>
    Task AddBlockExceptionAsync(CalendarBlockException exception, CalendarBlock? replacement, CancellationToken ct);

    /// <summary>A date's day override, tracked so <see cref="SaveAsync"/> saves changes to it.</summary>
    Task<CalendarDayOverride?> GetDayOverrideForUpdateAsync(Guid staffUserId, DateOnly date, CancellationToken ct);

    Task AddDayOverrideAsync(CalendarDayOverride dayOverride, CancellationToken ct);

    /// <summary>Deletes a day override loaded with <see cref="GetDayOverrideForUpdateAsync"/>.</summary>
    Task DeleteDayOverrideAsync(CalendarDayOverride dayOverride, CancellationToken ct);

    /// <summary>Saves changes to blocks and day overrides loaded for update.</summary>
    Task SaveAsync(CancellationToken ct);
}
