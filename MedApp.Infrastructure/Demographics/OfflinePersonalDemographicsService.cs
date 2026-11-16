using MedApp.Application.Demographics.Common;

namespace MedApp.Infrastructure.Demographics;

/// <summary>
/// A stand-in for the Personal Demographics Service that needs no network: it knows only a few synthetic people, each
/// with an NHS number from the 999 range, which is never issued to anyone real. For demonstrations and tests, when
/// <c>Pds:Mode</c> is <c>Offline</c>.
/// </summary>
public sealed class OfflinePersonalDemographicsService : IPersonalDemographicsService
{
    public const string SourceName = "Offline test data (synthetic 999 numbers)";

    /// <summary>Everyone the stub knows. Synthetic: the names are made up and the numbers are test numbers.</summary>
    public static readonly IReadOnlyList<DemographicsRecord> People =
    [
        new("9990000018", "Ada", "Synthetic", new DateOnly(1980, 5, 17)),
        new("9990000026", "Ben", "Testcase", new DateOnly(1955, 11, 2)),
        new("9990000034", "Cara", "Example", new DateOnly(2012, 3, 30)),
        new("9990000042", "Dev", "Placeholder", new DateOnly(1999, 7, 14)),
    ];

    public bool IsAvailable => true;

    public string Source => SourceName;

    public Task<DemographicsRecord?> LookUpAsync(string nhsNumber, CancellationToken cancellationToken) =>
        Task.FromResult(People.FirstOrDefault(p => p.NhsNumber == nhsNumber));
}
