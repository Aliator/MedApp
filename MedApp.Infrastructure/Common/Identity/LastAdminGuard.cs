using System.Data;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Common.Identity;

/// <summary>
/// Counts the active admins (<see cref="RolePermissionQueries.ActiveAdmins"/>) under a SQL Server application lock held
/// by the caller's transaction (<c>sp_getapplock</c>, as <see cref="Data.CalendarLock"/>), so the check and the change
/// it guards can't interleave with another admin's.
/// </summary>
public sealed class LastAdminGuard(MedAppDbContext context, TimeProvider timeProvider) : ILastAdminGuard
{
    public const string Busy = "Another change to admin accounts is being saved; please try again.";

    private const string Resource = "medapp-admins";
    private const int LockTimeoutMilliseconds = 15000;

    public async Task<bool> WouldLeaveNoAdminAsync(Guid userId, CancellationToken ct)
    {
        // Without the change's transaction there's no lock, and two admins could remove each other at once: refuse to
        // check at all, so a caller can't skip it by mistake.
        if (context.Database.IsSqlServer())
        {
            if (context.Database.CurrentTransaction is null)
                throw new InvalidOperationException(
                    "The last-admin check must run inside the change's transaction (IIdentityTransactions.RunAsync).");
            await LockAsync(ct);
        }

        var active = await RolePermissionQueries.ActiveAdmins(context, timeProvider.GetUtcNow()).ToListAsync(ct);
        return active.Contains(userId) && active.Count == 1;
    }

    private async Task LockAsync(CancellationToken ct)
    {
        var resource = new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = Resource };
        var timeout = new SqlParameter("@timeout", SqlDbType.Int) { Value = LockTimeoutMilliseconds };
        var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };

        await context.Database.ExecuteSqlRawAsync(
            "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = @timeout;",
            [resource, timeout, result],
            ct);

        if (result.Value is not int code || code < 0)
            throw new ConflictException(Busy);
    }
}
