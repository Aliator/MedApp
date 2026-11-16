using MedApp.Application.Common.Identity;
using Microsoft.AspNetCore.Identity;

namespace MedApp.UnitTests.Common.Fixtures;

/// <summary>Runs the work once, as a transaction would, and records that it was asked to (no database here).</summary>
public sealed class PassThroughIdentityTransactions : IIdentityTransactions
{
    public int Runs { get; private set; }

    public CancellationToken LastToken { get; private set; }

    public Task<IdentityResult> RunAsync(Func<Task<IdentityResult>> work, CancellationToken ct)
    {
        Runs++;
        LastToken = ct;
        return work();
    }
}
