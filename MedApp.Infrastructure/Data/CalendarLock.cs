using System.Data;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data;

/// <summary>
/// Serialises changes to members of staff's calendars with SQL Server application locks (<c>sp_getapplock</c>), one per
/// member of staff, held by a transaction: the work's clash check and save happen while no other request can change
/// that calendar, so two bookings racing for one slot can't both succeed. The repositories share this scope's
/// DbContext, so their queries and saves join the transaction.
/// </summary>
public sealed class CalendarLock(MedAppDbContext context) : ICalendarLock
{
    public const string Busy = "The calendar is busy; please try again.";

    /// <summary>How long to wait for another request's lock before giving up with a 409.</summary>
    public const int LockTimeoutMilliseconds = 15000;

    public async Task<T> RunAsync<T>(IReadOnlyCollection<Guid> staffUserIds, Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        // As IdentityTransactions: the DbContext retries transient failures, and EF only allows a transaction of our
        // own inside its execution strategy, which then retries it as a whole. A failed attempt is rolled back (and
        // its locks released), and its entities forgotten, so the work starts again from the database as it is.
        var strategy = context.Database.CreateExecutionStrategy();
        var attempt = 0;

        return await strategy.ExecuteAsync(async cancellationToken =>
        {
            if (attempt++ > 0)
                context.ChangeTracker.Clear();

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            // Always in the same order, so two requests locking the same people can't deadlock.
            foreach (var staffUserId in staffUserIds.Distinct().Order())
                await LockAsync(staffUserId, cancellationToken);

            // An exception (a clash, say) disposes the transaction without committing: it's rolled back.
            var result = await work(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return result;
        }, ct);
    }

    private async Task LockAsync(Guid staffUserId, CancellationToken ct)
    {
        var resource = new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = $"medapp-calendar-{staffUserId:D}" };
        var timeout = new SqlParameter("@timeout", SqlDbType.Int) { Value = LockTimeoutMilliseconds };
        var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };

        await context.Database.ExecuteSqlRawAsync(
            "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = @timeout;",
            [resource, timeout, result],
            ct);

        // 0 or 1: granted (at once, or after waiting). Below 0: timed out, cancelled, a deadlock victim or an error.
        if (result.Value is not int code || code < 0)
            throw new ConflictException(Busy);
    }
}
