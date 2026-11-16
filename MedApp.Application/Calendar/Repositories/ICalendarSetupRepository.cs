using MedApp.Application.Calendar.Common;
using MedApp.Domain.Calendar;

namespace MedApp.Application.Calendar.Repositories;

/// <summary>How much a day template is used.</summary>
/// <param name="WeekTemplateCount">Week templates that use it now.</param>
/// <param name="HasBeenUsed">Whether any week template or day override has ever referred to it.</param>
public sealed record DayTemplateUsage(int WeekTemplateCount, bool HasBeenUsed)
{
    public static readonly DayTemplateUsage None = new(0, false);
}

/// <summary>How much a week template is used.</summary>
/// <param name="StaffCount">People who have it as their default now.</param>
/// <param name="HasBeenUsed">Whether it has ever been anyone's default.</param>
public sealed record WeekTemplateUsage(int StaffCount, bool HasBeenUsed)
{
    public static readonly WeekTemplateUsage None = new(0, false);
}

/// <summary>
/// What admins set calendars up with: activity types, day templates and week templates. Templates' dated rows change
/// through <see cref="TemplateHistory"/>; "now" means the rows valid on the UK date given as today.
/// </summary>
public interface ICalendarSetupRepository
{
    /// <summary>Activity types sorted by name; archived ones only if asked.</summary>
    Task<IReadOnlyList<CalendarActivityType>> GetActivityTypesAsync(bool includeArchived, CancellationToken ct);

    Task<CalendarActivityType?> GetActivityTypeAsync(Guid id, CancellationToken ct);

    /// <summary>The activity types with these IDs (each once), archived or not.</summary>
    Task<IReadOnlyList<CalendarActivityType>> GetActivityTypesByIdAsync(IEnumerable<Guid> ids, CancellationToken ct);

    /// <summary>Whether another activity type has this name, ignoring case.</summary>
    Task<bool> ActivityTypeNameExistsAsync(string name, Guid? exceptId, CancellationToken ct);

    /// <summary>Which of these activity types a block or a template block (of any date) uses.</summary>
    Task<IReadOnlySet<Guid>> GetActivityTypesInUseAsync(IEnumerable<Guid> ids, CancellationToken ct);

    Task AddActivityTypeAsync(CalendarActivityType activityType, CancellationToken ct);
    Task UpdateActivityTypeAsync(CalendarActivityType activityType, CancellationToken ct);
    Task DeleteActivityTypeAsync(Guid id, CancellationToken ct);

    /// <summary>Day templates sorted by name, each with its blocks valid on <paramref name="today"/>; archived ones only if asked.</summary>
    Task<IReadOnlyList<DayTemplate>> GetDayTemplatesAsync(bool includeArchived, DateOnly today, CancellationToken ct);

    /// <summary>A day template with its blocks valid on <paramref name="today"/> (each with its activity type), read-only.</summary>
    Task<DayTemplate?> GetDayTemplateAsync(Guid id, DateOnly today, CancellationToken ct);

    /// <summary>
    /// A day template with its blocks valid on <paramref name="today"/> (each with its activity type), tracked so
    /// <see cref="SaveDayTemplateAsync"/> saves changes to it.
    /// </summary>
    Task<DayTemplate?> GetDayTemplateForUpdateAsync(Guid id, DateOnly today, CancellationToken ct);

    /// <summary>The day templates with these IDs (each once), archived or not, without their blocks.</summary>
    Task<IReadOnlyList<DayTemplate>> GetDayTemplatesByIdAsync(IEnumerable<Guid> ids, CancellationToken ct);

    /// <summary>Whether another day template has this name, ignoring case.</summary>
    Task<bool> DayTemplateNameExistsAsync(string name, Guid? exceptId, CancellationToken ct);

    /// <summary>How much each of these day templates is used; one never used has no entry.</summary>
    Task<IReadOnlyDictionary<Guid, DayTemplateUsage>> GetDayTemplateUsageAsync(IEnumerable<Guid> ids, DateOnly today, CancellationToken ct);

    /// <summary>Adds a day template with its blocks.</summary>
    Task AddDayTemplateAsync(DayTemplate template, CancellationToken ct);

    /// <summary>Saves a day template loaded with <see cref="GetDayTemplateForUpdateAsync"/>, and the change to its blocks, if any.</summary>
    Task SaveDayTemplateAsync(DayTemplate template, TemplateHistoryChange<DayTemplateBlock>? blocks, CancellationToken ct);

    /// <summary>Deletes a day template with its blocks (and any exceptions to them).</summary>
    Task DeleteDayTemplateAsync(Guid id, CancellationToken ct);

    /// <summary>Week templates sorted by name, each with its days valid on <paramref name="today"/>; archived ones only if asked.</summary>
    Task<IReadOnlyList<WeekTemplate>> GetWeekTemplatesAsync(bool includeArchived, DateOnly today, CancellationToken ct);

    /// <summary>A week template with its days valid on <paramref name="today"/> (each with its day template), read-only.</summary>
    Task<WeekTemplate?> GetWeekTemplateAsync(Guid id, DateOnly today, CancellationToken ct);

    /// <summary>
    /// A week template with its days valid on <paramref name="today"/> (each with its day template), tracked so
    /// <see cref="SaveWeekTemplateAsync"/> saves changes to it.
    /// </summary>
    Task<WeekTemplate?> GetWeekTemplateForUpdateAsync(Guid id, DateOnly today, CancellationToken ct);

    /// <summary>Whether another week template has this name, ignoring case.</summary>
    Task<bool> WeekTemplateNameExistsAsync(string name, Guid? exceptId, CancellationToken ct);

    /// <summary>How much each of these week templates is used; one never used has no entry.</summary>
    Task<IReadOnlyDictionary<Guid, WeekTemplateUsage>> GetWeekTemplateUsageAsync(IEnumerable<Guid> ids, DateOnly today, CancellationToken ct);

    /// <summary>Adds a week template with its days.</summary>
    Task AddWeekTemplateAsync(WeekTemplate template, CancellationToken ct);

    /// <summary>Saves a week template loaded with <see cref="GetWeekTemplateForUpdateAsync"/>, and the change to its days, if any.</summary>
    Task SaveWeekTemplateAsync(WeekTemplate template, TemplateHistoryChange<WeekTemplateDay>? days, CancellationToken ct);

    /// <summary>Deletes a week template with its days.</summary>
    Task DeleteWeekTemplateAsync(Guid id, CancellationToken ct);
}
