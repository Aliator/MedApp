using MedApp.Application.Calendar.Common;
using MedApp.Domain.Calendar;
using MedApp.Domain.Sites;

namespace MedApp.Application.Calendar.Repositories;

/// <summary>
/// Staff working hours and site opening hours (centre plan decision 119), dated rows kept with
/// <see cref="TemplateHistory"/>. Changes run inside <see cref="ICalendarLock"/>, whose transaction these share.
/// </summary>
public interface IWorkingHoursRepository
{
    /// <summary>
    /// These people's hours valid on any date from <paramref name="from"/> to <paramref name="to"/>, with the hours of
    /// every site they name, read-only.
    /// </summary>
    Task<WorkingTimeSet> GetAsync(IReadOnlyCollection<Guid> staffUserIds, DateOnly from, DateOnly to, CancellationToken ct);

    /// <summary>A person's hours valid on a date, read-only.</summary>
    Task<IReadOnlyList<StaffHours>> GetStaffHoursOnAsync(Guid staffUserId, DateOnly date, CancellationToken ct);

    /// <summary>A person's hours valid on <paramref name="today"/> or later, tracked for <see cref="SaveStaffHoursAsync"/>.</summary>
    Task<IReadOnlyList<StaffHours>> GetStaffHoursForUpdateAsync(Guid staffUserId, DateOnly today, CancellationToken ct);

    Task SaveStaffHoursAsync(TemplateHistoryChange<StaffHours> change, CancellationToken ct);

    /// <summary>Everyone's hours at a site valid on <paramref name="today"/> or later, read-only.</summary>
    Task<IReadOnlyList<StaffHours>> GetStaffHoursAtSiteAsync(Guid siteId, DateOnly today, CancellationToken ct);

    /// <summary>A site's hours valid on a date, read-only.</summary>
    Task<IReadOnlyList<SiteHours>> GetSiteHoursOnAsync(Guid siteId, DateOnly date, CancellationToken ct);

    /// <summary>These sites' hours valid on a date, read-only.</summary>
    Task<IReadOnlyList<SiteHours>> GetSiteHoursOnAsync(IReadOnlyCollection<Guid> siteIds, DateOnly date, CancellationToken ct);

    /// <summary>A site's hours valid on <paramref name="today"/> or later, tracked for <see cref="SaveSiteHoursAsync"/>.</summary>
    Task<IReadOnlyList<SiteHours>> GetSiteHoursForUpdateAsync(Guid siteId, DateOnly today, CancellationToken ct);

    Task SaveSiteHoursAsync(TemplateHistoryChange<SiteHours> change, CancellationToken ct);
}
