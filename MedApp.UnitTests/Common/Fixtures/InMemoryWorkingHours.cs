using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Domain.Calendar;
using MedApp.Domain.Sites;

namespace MedApp.UnitTests.Common.Fixtures;

/// <summary>
/// Staff and site hours kept in memory, for <see cref="WorkingDays"/> and handlers: none unless given, so everyone works
/// the default day. Its tasks are already complete, so validators using it can run without awaiting.
/// </summary>
public sealed class InMemoryWorkingHours : IWorkingHoursRepository
{
    public List<StaffHours> Staff { get; } = [];

    public List<SiteHours> Sites { get; } = [];

    /// <summary>A person's hours on a day of the week from a date, at a site.</summary>
    public static StaffHours StaffRow(
        Guid staffUserId, DayOfWeek day, TimeOnly starts, TimeOnly ends, Guid siteId, DateOnly? validFrom = null, DateOnly? validTo = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            StaffUserId = staffUserId,
            DayOfWeek = day,
            Starts = starts,
            Ends = ends,
            SiteId = siteId,
            ValidFrom = validFrom ?? DateOnly.MinValue,
            ValidTo = validTo
        };

    /// <summary>A site's hours on a day of the week from a date.</summary>
    public static SiteHours SiteRow(
        Guid siteId, DayOfWeek day, TimeOnly opens, TimeOnly closes, DateOnly? validFrom = null, DateOnly? validTo = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            SiteId = siteId,
            DayOfWeek = day,
            Opens = opens,
            Closes = closes,
            ValidFrom = validFrom ?? DateOnly.MinValue,
            ValidTo = validTo
        };

    public Task<WorkingTimeSet> GetAsync(IReadOnlyCollection<Guid> staffUserIds, DateOnly from, DateOnly to, CancellationToken ct) =>
        Task.FromResult(new WorkingTimeSet(
            Staff.Where(h => staffUserIds.Contains(h.StaffUserId) && Overlaps(h, from, to)),
            Sites.Where(h => Overlaps(h, from, to))));

    public Task<IReadOnlyList<StaffHours>> GetStaffHoursOnAsync(Guid staffUserId, DateOnly date, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StaffHours>>(Staff.Where(h => h.StaffUserId == staffUserId && h.IsValidOn(date)).ToList());

    public Task<IReadOnlyList<StaffHours>> GetStaffHoursForUpdateAsync(Guid staffUserId, DateOnly today, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StaffHours>>(Staff.Where(h => h.StaffUserId == staffUserId && (h.ValidTo is null || h.ValidTo >= today)).ToList());

    public Task SaveStaffHoursAsync(TemplateHistoryChange<StaffHours> change, CancellationToken ct)
    {
        Apply(Staff, change);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StaffHours>> GetStaffHoursAtSiteAsync(Guid siteId, DateOnly today, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StaffHours>>(Staff.Where(h => h.SiteId == siteId && (h.ValidTo is null || h.ValidTo >= today)).ToList());

    public Task<IReadOnlyList<SiteHours>> GetSiteHoursOnAsync(Guid siteId, DateOnly date, CancellationToken ct) =>
        GetSiteHoursOnAsync([siteId], date, ct);

    public Task<IReadOnlyList<SiteHours>> GetSiteHoursOnAsync(IReadOnlyCollection<Guid> siteIds, DateOnly date, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SiteHours>>(Sites.Where(h => siteIds.Contains(h.SiteId) && h.IsValidOn(date)).ToList());

    public Task<IReadOnlyList<SiteHours>> GetSiteHoursForUpdateAsync(Guid siteId, DateOnly today, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SiteHours>>(Sites.Where(h => h.SiteId == siteId && (h.ValidTo is null || h.ValidTo >= today)).ToList());

    public Task SaveSiteHoursAsync(TemplateHistoryChange<SiteHours> change, CancellationToken ct)
    {
        Apply(Sites, change);
        return Task.CompletedTask;
    }

    private static bool Overlaps(IDatedRow row, DateOnly from, DateOnly to) =>
        row.ValidFrom <= to && (row.ValidTo is null || row.ValidTo >= from);

    private static void Apply<TRow>(List<TRow> rows, TemplateHistoryChange<TRow> change) where TRow : IDatedRow
    {
        foreach (var row in change.Close)
            row.ValidTo = change.ClosedValidTo;
        foreach (var row in change.Delete)
            rows.Remove(row);
        rows.AddRange(change.Add);
    }
}
