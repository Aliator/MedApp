namespace MedApp.Application.Demographics.Common;

/// <summary>
/// Looks people up by NHS number in a demographics service, such as NHS England's Personal Demographics Service (PDS).
/// Only an offline stub exists (<c>OfflinePersonalDemographicsService</c>, a few synthetic 999 numbers), and it's off
/// unless configured (<see cref="PdsOptions"/>). The real PDS FHIR API needs NHS England's onboarding first, so it isn't
/// wired in: see <c>docs/governance/nhs-context.md</c>.
/// </summary>
public interface IPersonalDemographicsService
{
    /// <summary>Whether a lookup is set up. When it isn't, nothing offers one.</summary>
    bool IsAvailable { get; }

    /// <summary>What the service is, in words, e.g. "Offline test data (synthetic 999 numbers)"; null when unavailable.</summary>
    string? Source { get; }

    /// <summary>The person with this NHS number (ten digits, already checked), or null when the service has none.</summary>
    Task<DemographicsRecord?> LookUpAsync(string nhsNumber, CancellationToken cancellationToken);
}

/// <summary>What a demographics service says about someone: enough to fill in a new patient's form.</summary>
public sealed record DemographicsRecord(string NhsNumber, string FirstName, string LastName, DateOnly DateOfBirth);
