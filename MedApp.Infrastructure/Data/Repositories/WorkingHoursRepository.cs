using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Domain.Calendar;
using MedApp.Domain.Sites;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>
/// Staff and site hours. Changes run inside <c>CalendarLock</c>'s transaction, which these queries and saves join, as
/// they share the request's DbContext.
/// </summary>
public sealed class WorkingHoursRepository(MedAppDbContext context) : IWorkingHoursRepository
{
    public async Task<WorkingTimeSet> GetAsync(
        IReadOnlyCollection<Guid> staffUserIds, DateOnly from, DateOnly to, CancellationToken ct) =>
        await LoadAsync(context, staffUserIds, from, to, ct);

    /// <summary>These people's hours over a range, with their sites' (for the calendar repository too).</summary>
    internal static async Task<WorkingTimeSet> LoadAsync(
        MedAppDbContext context, IReadOnlyCollection<Guid> staffUserIds, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var ids = staffUserIds.Distinct().ToList();
        var staffHours = await context.StaffHours
            .AsNoTracking()
            .Where(h => ids.Contains(h.StaffUserId) && h.ValidFrom <= to && (h.ValidTo == null || h.ValidTo >= from))
            .ToListAsync(ct);
        if (staffHours.Count == 0)
            return WorkingTimeSet.None;

        var siteIds = staffHours.Select(h => h.SiteId).Distinct().ToList();
        var siteHours = await context.SiteHours
            .AsNoTracking()
            .Where(h => siteIds.Contains(h.SiteId) && h.ValidFrom <= to && (h.ValidTo == null || h.ValidTo >= from))
            .ToListAsync(ct);
        return new WorkingTimeSet(staffHours, siteHours);
    }

    public async Task<IReadOnlyList<StaffHours>> GetStaffHoursOnAsync(Guid staffUserId, DateOnly date, CancellationToken ct) =>
        await context.StaffHours
            .AsNoTracking()
            .Where(h => h.StaffUserId == staffUserId && h.ValidFrom <= date && (h.ValidTo == null || h.ValidTo >= date))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<StaffHours>> GetStaffHoursForUpdateAsync(Guid staffUserId, DateOnly today, CancellationToken ct) =>
        await context.StaffHours
            .Where(h => h.StaffUserId == staffUserId && (h.ValidTo == null || h.ValidTo >= today))
            .ToListAsync(ct);

    public async Task SaveStaffHoursAsync(TemplateHistoryChange<StaffHours> change, CancellationToken ct)
    {
        CalendarSetupRepository.Apply(context.StaffHours, change);
        await context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<StaffHours>> GetStaffHoursAtSiteAsync(Guid siteId, DateOnly today, CancellationToken ct) =>
        await context.StaffHours
            .AsNoTracking()
            .Where(h => h.SiteId == siteId && (h.ValidTo == null || h.ValidTo >= today))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SiteHours>> GetSiteHoursOnAsync(Guid siteId, DateOnly date, CancellationToken ct) =>
        await GetSiteHoursOnAsync([siteId], date, ct);

    public async Task<IReadOnlyList<SiteHours>> GetSiteHoursOnAsync(
        IReadOnlyCollection<Guid> siteIds, DateOnly date, CancellationToken ct)
    {
        var ids = siteIds.Distinct().ToList();
        return await context.SiteHours
            .AsNoTracking()
            .Where(h => ids.Contains(h.SiteId) && h.ValidFrom <= date && (h.ValidTo == null || h.ValidTo >= date))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SiteHours>> GetSiteHoursForUpdateAsync(Guid siteId, DateOnly today, CancellationToken ct) =>
        await context.SiteHours
            .Where(h => h.SiteId == siteId && (h.ValidTo == null || h.ValidTo >= today))
            .ToListAsync(ct);

    public async Task SaveSiteHoursAsync(TemplateHistoryChange<SiteHours> change, CancellationToken ct)
    {
        CalendarSetupRepository.Apply(context.SiteHours, change);
        await context.SaveChangesAsync(ct);
    }
}
