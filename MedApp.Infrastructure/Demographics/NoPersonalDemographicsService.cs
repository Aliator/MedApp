using MedApp.Application.Demographics.Common;

namespace MedApp.Infrastructure.Demographics;

/// <summary>No demographics lookup: the default, when <c>Pds:Mode</c> is <c>Off</c> or not set.</summary>
public sealed class NoPersonalDemographicsService : IPersonalDemographicsService
{
    public bool IsAvailable => false;

    public string? Source => null;

    public Task<DemographicsRecord?> LookUpAsync(string nhsNumber, CancellationToken cancellationToken) =>
        Task.FromResult<DemographicsRecord?>(null);
}
