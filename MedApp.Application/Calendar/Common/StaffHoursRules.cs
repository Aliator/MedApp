using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Sites.Repositories;
using MedApp.Contracts.Calendar.Responses;

namespace MedApp.Application.Calendar.Common;

/// <summary>What staff hours must be against their sites' hours, and how they're read back.</summary>
public static class StaffHoursRules
{
    public static string SiteClosed(string siteName, DayOfWeek day) =>
        $"{siteName} isn't open on {day}s, so nobody can work there then. Change its opening hours first.";

    public static string OutsideSite(string siteName, DayOfWeek day, TimeOnly opens, TimeOnly closes) =>
        $"{siteName} is open {WorkingHours.Describe(opens)} to {WorkingHours.Describe(closes)} on {day}s: working hours there must be inside them.";

    /// <summary>A person's hours valid today, with their sites' names.</summary>
    public static async Task<StaffHoursResponse> ToResponseAsync(
        Guid staffUserId,
        DateOnly today,
        bool canEdit,
        IWorkingHoursRepository repository,
        ISiteRepository siteRepository,
        CancellationToken ct)
    {
        var rows = await repository.GetStaffHoursOnAsync(staffUserId, today, ct);
        var siteNames = rows.Count == 0
            ? new Dictionary<Guid, string>()
            : (await siteRepository.GetSitesAsync(includeArchived: true, ct)).ToDictionary(s => s.Id, s => s.Name);
        return WorkingHoursMapper.ToResponse(staffUserId, rows, siteNames, canEdit);
    }
}
