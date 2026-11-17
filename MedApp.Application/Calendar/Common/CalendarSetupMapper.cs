using MedApp.Application.Calendar.Repositories;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Domain.Calendar;

namespace MedApp.Application.Calendar.Common;

/// <summary>
/// What calendars are set up with, as the API returns them: activity types, and each person's day templates and week
/// templates (<c>canEdit</c>: whether the signed-in user may change them, as their owner or an admin).
/// </summary>
public static class CalendarSetupMapper
{
    /// <summary>The weekdays a week template has, in order.</summary>
    public static readonly IReadOnlyList<DayOfWeek> Weekdays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    /// <param name="inUse">Whether a block or template block uses it, so it can't be deleted.</param>
    public static ActivityTypeResponse ToResponse(CalendarActivityType activityType, bool inUse) => new()
    {
        Id = activityType.Id,
        Name = activityType.Name,
        Colour = activityType.Colour,
        IsArchived = activityType.IsArchived,
        CanBeDeleted = !inUse
    };

    /// <summary>The response for one activity type, looking up whether it's in use.</summary>
    public static async Task<ActivityTypeResponse> ToResponseAsync(
        CalendarActivityType activityType,
        ICalendarSetupRepository repository,
        CancellationToken ct)
    {
        var inUse = await repository.GetActivityTypesInUseAsync([activityType.Id], ct);
        return ToResponse(activityType, inUse.Contains(activityType.Id));
    }

    /// <summary>The response for one day template, looking up how much it's used.</summary>
    public static async Task<DayTemplateResponse> ToResponseAsync(
        DayTemplate template,
        ICalendarSetupRepository repository,
        DateOnly today,
        bool canEdit,
        CancellationToken ct)
    {
        var usage = await repository.GetDayTemplateUsageAsync([template.Id], today, ct);
        return ToResponse(template, usage.GetValueOrDefault(template.Id, DayTemplateUsage.None), today, canEdit);
    }

    /// <summary>The response for one week template, looking up how much it's used.</summary>
    public static async Task<WeekTemplateResponse> ToResponseAsync(
        WeekTemplate template,
        ICalendarSetupRepository repository,
        DateOnly today,
        bool canEdit,
        CancellationToken ct)
    {
        var usage = await repository.GetWeekTemplateUsageAsync([template.Id], today, ct);
        return ToResponse(template, usage.GetValueOrDefault(template.Id, WeekTemplateUsage.None), today, canEdit);
    }

    /// <summary>A day template with its blocks valid on <paramref name="today"/> (its current version), by start time.</summary>
    public static DayTemplateResponse ToResponse(DayTemplate template, DayTemplateUsage usage, DateOnly today, bool canEdit) => new()
    {
        Id = template.Id,
        OwnerUserId = template.OwnerUserId,
        Name = template.Name,
        IsArchived = template.IsArchived,
        Blocks = template.Blocks
            .Where(b => b.IsValidOn(today))
            .OrderBy(b => b.StartTime)
            .ThenBy(b => b.Id)
            .Select(ToResponse)
            .ToList(),
        WeekTemplateCount = usage.WeekTemplateCount,
        CanBeDeleted = !usage.HasBeenUsed,
        CanEdit = canEdit
    };

    public static DayTemplateBlockResponse ToResponse(DayTemplateBlock block) => new()
    {
        Id = block.Id,
        ActivityTypeId = block.ActivityTypeId,
        ActivityName = block.ActivityType?.Name ?? string.Empty,
        Colour = block.ActivityType?.Colour ?? string.Empty,
        StartTime = block.StartTime,
        EndTime = block.EndTime,
        Note = block.Note
    };

    /// <summary>A week template with its day template for each weekday valid on <paramref name="today"/>, Monday to Friday.</summary>
    public static WeekTemplateResponse ToResponse(WeekTemplate template, WeekTemplateUsage usage, DateOnly today, bool canEdit) => new()
    {
        Id = template.Id,
        OwnerUserId = template.OwnerUserId,
        Name = template.Name,
        IsArchived = template.IsArchived,
        Days = Weekdays
            .Select(weekday =>
            {
                var day = template.Days
                    .Where(d => d.DayOfWeek == weekday && d.IsValidOn(today))
                    .MaxBy(d => d.ValidFrom);
                return new WeekTemplateDayResponse
                {
                    DayOfWeek = weekday.ToString(),
                    DayTemplateId = day?.DayTemplateId,
                    DayTemplateName = day?.DayTemplate?.Name
                };
            })
            .ToList(),
        StaffCount = usage.StaffCount,
        CanBeDeleted = !usage.HasBeenUsed,
        CanEdit = canEdit
    };
}
