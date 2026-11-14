using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Common.Identity;

/// <summary>
/// Runs several Identity changes (each of which saves on its own) as one: they're all kept if the work succeeds,
/// and all undone if it fails, so nothing is left half-changed.
/// </summary>
public interface IIdentityTransactions
{
    Task<IdentityResult> RunAsync(Func<Task<IdentityResult>> work, CancellationToken ct);
}
