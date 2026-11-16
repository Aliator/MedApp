using MedApp.Application.Common.Identity;
using MedApp.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Common.Identity;

/// <summary>
/// Runs Identity changes in one database transaction. Identity's managers share this scope's DbContext, so their
/// saves join it: committed when the work succeeds, rolled back when it fails or throws.
/// </summary>
public sealed class IdentityTransactions(MedAppDbContext context) : IIdentityTransactions
{
    public async Task<IdentityResult> RunAsync(Func<Task<IdentityResult>> work, CancellationToken ct)
    {
        // The DbContext retries on transient failures (ServiceExtensions), and EF only allows a transaction of our own
        // inside its execution strategy, which then retries the transaction as a whole: begin, the work, and the
        // commit or rollback. A failed attempt's transaction is rolled back, so each attempt starts from the database
        // as it was.
        //
        // So the work must load everything it changes itself, inside the delegate, and callers mustn't capture
        // entities loaded before it: a retry detaches them (below), and the failed attempt may already have changed
        // them in memory (Identity gives a user a new concurrency stamp before saving), so the retry would fail with
        // a false concurrency error. IdentityRoleService.SetRolesAsync and FirstAdminSetup follow this.
        var strategy = context.Database.CreateExecutionStrategy();
        var attempt = 0;

        return await strategy.ExecuteAsync(async cancellationToken =>
        {
            // A retry runs the work again from the start. The failed attempt's entities are still tracked (as added,
            // or changed), and the work adds its own again, so forget them first: otherwise Identity would save the
            // same roles and users twice.
            if (attempt++ > 0)
                context.ChangeTracker.Clear();

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            var result = await work();

            if (result.Succeeded)
                await transaction.CommitAsync(cancellationToken);
            else
                await transaction.RollbackAsync(cancellationToken);

            return result;
        }, ct);
    }
}
