using MedApp.Application.Common.Identity;
using MedApp.Domain.Activity;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Common.Identity;

/// <summary>
/// Staff members' full names and job titles: two columns on Identity's users table, written directly (not through the
/// user manager, which would change the security stamp), with the activity log's entry in the same save.
/// </summary>
public sealed class StaffDetailsService(MedAppDbContext context) : IStaffDetailsService
{
    public async Task<StaffDetails?> GetAsync(Guid userId, CancellationToken ct) =>
        await context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new StaffDetails(u.FullName, u.JobTitle))
            .FirstOrDefaultAsync(ct);

    public async Task<bool> SetAsync(Guid userId, StaffDetails details, ActivityEntry activity, CancellationToken ct)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return false;

        user.FullName = details.FullName;
        user.JobTitle = details.JobTitle;
        // As the user manager does on every update, so a change it makes from a copy read before this one is refused.
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        context.ActivityEntries.Add(activity);

        await context.SaveChangesOrConflictAsync(
            "Someone changed this account at the same moment. Reload to see it, then try again.", ct);
        return true;
    }
}
