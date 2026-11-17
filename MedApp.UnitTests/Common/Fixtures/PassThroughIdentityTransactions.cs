using MedApp.Application.Common.Identity;
using Microsoft.AspNetCore.Identity;

namespace MedApp.UnitTests.Common.Fixtures;

/// <summary>
/// Runs the work once, as a transaction would, and records that it was asked to (no database here), and whether the
/// work is running inside it now (<see cref="IsRunning"/>), so a test can check what a handler does inside it.
/// </summary>
public sealed class PassThroughIdentityTransactions : IIdentityTransactions
{
    public int Runs { get; private set; }

    public CancellationToken LastToken { get; private set; }

    public bool IsRunning { get; private set; }

    public async Task<IdentityResult> RunAsync(Func<Task<IdentityResult>> work, CancellationToken ct)
    {
        Runs++;
        LastToken = ct;
        IsRunning = true;
        try
        {
            return await work();
        }
        finally
        {
            IsRunning = false;
        }
    }
}
