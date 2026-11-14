using MedApp.Application.Common.Identity;
using MedApp.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Infrastructure.Common.Identity;

/// <summary>
/// Runs Identity changes in one database transaction. Identity's managers share this scope's DbContext, so their
/// saves join it: committed when the work succeeds, rolled back when it fails or throws.
/// </summary>
public sealed class IdentityTransactions(MedAppDbContext context) : IIdentityTransactions
{
    public async Task<IdentityResult> RunAsync(Func<Task<IdentityResult>> work, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);

        var result = await work();

        if (result.Succeeded)
            await transaction.CommitAsync(ct);
        else
            await transaction.RollbackAsync(ct);

        return result;
    }
}
