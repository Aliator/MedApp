using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Domain.Calendar;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>
/// Activity types, and each member of staff's day templates and week templates. Templates are loaded with their rows
/// valid on a date (the current version), and edited by applying a <see cref="TemplateHistoryChange{TRow}"/> to those rows.
/// </summary>
public sealed class CalendarSetupRepository(MedAppDbContext context) : ICalendarSetupRepository
{
    public async Task<IReadOnlyList<CalendarActivityType>> GetActivityTypesAsync(bool includeArchived, CancellationToken ct) =>
        await context.CalendarActivityTypes
            .AsNoTracking()
            .Where(x => includeArchived || !x.IsArchived)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

    public async Task<CalendarActivityType?> GetActivityTypeAsync(Guid id, CancellationToken ct) =>
        await context.CalendarActivityTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<CalendarActivityType>> GetActivityTypesByIdAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await context.CalendarActivityTypes.AsNoTracking().Where(x => list.Contains(x.Id)).ToListAsync(ct);
    }

    public async Task<bool> ActivityTypeNameExistsAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        var lower = name.Trim().ToLower();
        return await context.CalendarActivityTypes
            .AnyAsync(x => x.Name.ToLower() == lower && (exceptId == null || x.Id != exceptId), ct);
    }

    public async Task<IReadOnlySet<Guid>> GetActivityTypesInUseAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();

        var inBlocks = await context.CalendarBlocks
            .Where(b => list.Contains(b.ActivityTypeId))
            .Select(b => b.ActivityTypeId)
            .Distinct()
            .ToListAsync(ct);

        var inTemplates = await context.DayTemplateBlocks
            .Where(b => list.Contains(b.ActivityTypeId))
            .Select(b => b.ActivityTypeId)
            .Distinct()
            .ToListAsync(ct);

        return inBlocks.Concat(inTemplates).ToHashSet();
    }

    public async Task AddActivityTypeAsync(CalendarActivityType activityType, CancellationToken ct)
    {
        context.CalendarActivityTypes.Add(activityType);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateActivityTypeAsync(CalendarActivityType activityType, CancellationToken ct)
    {
        context.CalendarActivityTypes.Update(activityType);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteActivityTypeAsync(Guid id, CancellationToken ct)
    {
        var activityType = await context.CalendarActivityTypes.FindAsync([id], ct);
        if (activityType is null)
            return;

        context.CalendarActivityTypes.Remove(activityType);
        await context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DayTemplate>> GetDayTemplatesAsync(
        Guid ownerUserId, bool includeArchived, DateOnly today, CancellationToken ct) =>
        await WithBlocksOn(context.DayTemplates.AsNoTracking(), today)
            .Where(x => x.OwnerUserId == ownerUserId && (includeArchived || !x.IsArchived))
            .OrderBy(x => x.Name)
            .AsSplitQuery()
            .ToListAsync(ct);

    public async Task<DayTemplate?> GetDayTemplateAsync(Guid id, DateOnly today, CancellationToken ct) =>
        await WithBlocksOn(context.DayTemplates.AsNoTracking(), today).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<DayTemplate?> GetDayTemplateForUpdateAsync(Guid id, DateOnly today, CancellationToken ct) =>
        await WithBlocksOn(context.DayTemplates, today).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<DayTemplate>> GetDayTemplatesByIdAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await context.DayTemplates.AsNoTracking().Where(x => list.Contains(x.Id)).ToListAsync(ct);
    }

    public async Task<bool> DayTemplateNameExistsAsync(Guid ownerUserId, string name, Guid? exceptId, CancellationToken ct)
    {
        var lower = name.Trim().ToLower();
        return await context.DayTemplates.AnyAsync(
            x => x.OwnerUserId == ownerUserId && x.Name.ToLower() == lower && (exceptId == null || x.Id != exceptId), ct);
    }

    public async Task<IReadOnlyDictionary<Guid, DayTemplateUsage>> GetDayTemplateUsageAsync(
        IEnumerable<Guid> ids, DateOnly today, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();

        // Week templates that use it on any weekday today.
        var weekTemplateCounts = await context.WeekTemplateDays
            .Where(d => list.Contains(d.DayTemplateId) && d.ValidFrom <= today && (d.ValidTo == null || d.ValidTo >= today))
            .GroupBy(d => d.DayTemplateId)
            .Select(g => new { g.Key, Count = g.Select(d => d.WeekTemplateId).Distinct().Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        // Ever referred to, by a week template's day (of any date) or a day override.
        var everInWeeks = await context.WeekTemplateDays
            .Where(d => list.Contains(d.DayTemplateId))
            .Select(d => d.DayTemplateId)
            .Distinct()
            .ToListAsync(ct);
        var everInOverrides = await context.CalendarDayOverrides
            .Where(o => o.DayTemplateId != null && list.Contains(o.DayTemplateId.Value))
            .Select(o => o.DayTemplateId!.Value)
            .Distinct()
            .ToListAsync(ct);
        var used = everInWeeks.Concat(everInOverrides).ToHashSet();

        return list
            .Where(used.Contains)
            .ToDictionary(id => id, id => new DayTemplateUsage(weekTemplateCounts.GetValueOrDefault(id), HasBeenUsed: true));
    }

    public async Task AddDayTemplateAsync(DayTemplate template, CancellationToken ct)
    {
        context.DayTemplates.Add(template);
        await context.SaveChangesAsync(ct);
    }

    public async Task SaveDayTemplateAsync(DayTemplate template, TemplateHistoryChange<DayTemplateBlock>? blocks, CancellationToken ct)
    {
        if (blocks is not null)
            Apply(context.DayTemplateBlocks, blocks);

        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteDayTemplateAsync(Guid id, CancellationToken ct)
    {
        var template = await context.DayTemplates.FindAsync([id], ct);
        if (template is null)
            return;

        // Its blocks cascade, and the exceptions to them with them.
        context.DayTemplates.Remove(template);
        await context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<WeekTemplate>> GetWeekTemplatesAsync(
        Guid ownerUserId, bool includeArchived, DateOnly today, CancellationToken ct) =>
        await WithDaysOn(context.WeekTemplates.AsNoTracking(), today)
            .Where(x => x.OwnerUserId == ownerUserId && (includeArchived || !x.IsArchived))
            .OrderBy(x => x.Name)
            .AsSplitQuery()
            .ToListAsync(ct);

    public async Task<WeekTemplate?> GetWeekTemplateAsync(Guid id, DateOnly today, CancellationToken ct) =>
        await WithDaysOn(context.WeekTemplates.AsNoTracking(), today).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<WeekTemplate?> GetWeekTemplateForUpdateAsync(Guid id, DateOnly today, CancellationToken ct) =>
        await WithDaysOn(context.WeekTemplates, today).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<bool> WeekTemplateNameExistsAsync(Guid ownerUserId, string name, Guid? exceptId, CancellationToken ct)
    {
        var lower = name.Trim().ToLower();
        return await context.WeekTemplates.AnyAsync(
            x => x.OwnerUserId == ownerUserId && x.Name.ToLower() == lower && (exceptId == null || x.Id != exceptId), ct);
    }

    public async Task<IReadOnlyDictionary<Guid, WeekTemplateUsage>> GetWeekTemplateUsageAsync(
        IEnumerable<Guid> ids, DateOnly today, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();

        // People with it as their default today.
        var staffCounts = await context.StaffWeekTemplates
            .Where(s => list.Contains(s.WeekTemplateId) && s.ValidFrom <= today && (s.ValidTo == null || s.ValidTo >= today))
            .GroupBy(s => s.WeekTemplateId)
            .Select(g => new { g.Key, Count = g.Select(s => s.StaffUserId).Distinct().Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        // Ever anyone's default.
        var used = (await context.StaffWeekTemplates
                .Where(s => list.Contains(s.WeekTemplateId))
                .Select(s => s.WeekTemplateId)
                .Distinct()
                .ToListAsync(ct))
            .ToHashSet();

        return list
            .Where(used.Contains)
            .ToDictionary(id => id, id => new WeekTemplateUsage(staffCounts.GetValueOrDefault(id), HasBeenUsed: true));
    }

    public async Task AddWeekTemplateAsync(WeekTemplate template, CancellationToken ct)
    {
        context.WeekTemplates.Add(template);
        await context.SaveChangesAsync(ct);
    }

    public async Task SaveWeekTemplateAsync(WeekTemplate template, TemplateHistoryChange<WeekTemplateDay>? days, CancellationToken ct)
    {
        if (days is not null)
            Apply(context.WeekTemplateDays, days);

        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteWeekTemplateAsync(Guid id, CancellationToken ct)
    {
        var template = await context.WeekTemplates.FindAsync([id], ct);
        if (template is null)
            return;

        // Its days cascade.
        context.WeekTemplates.Remove(template);
        await context.SaveChangesAsync(ct);
    }

    /// <summary>Applies a change to tracked dated rows: closes, deletes and adds. The caller saves.</summary>
    internal static void Apply<TRow>(DbSet<TRow> rows, TemplateHistoryChange<TRow> change) where TRow : class, IDatedRow
    {
        foreach (var row in change.Close)
            row.ValidTo = change.ClosedValidTo;

        rows.RemoveRange(change.Delete);
        rows.AddRange(change.Add);
    }

    private static IQueryable<DayTemplate> WithBlocksOn(IQueryable<DayTemplate> templates, DateOnly date) =>
        templates
            .Include(x => x.Blocks
                .Where(b => b.ValidFrom <= date && (b.ValidTo == null || b.ValidTo >= date))
                .OrderBy(b => b.StartTime))
            .ThenInclude(b => b.ActivityType);

    private static IQueryable<WeekTemplate> WithDaysOn(IQueryable<WeekTemplate> templates, DateOnly date) =>
        templates
            .Include(x => x.Days.Where(d => d.ValidFrom <= date && (d.ValidTo == null || d.ValidTo >= date)))
            .ThenInclude(d => d.DayTemplate);
}
