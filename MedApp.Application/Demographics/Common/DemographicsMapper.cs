using MedApp.Contracts.Demographics.Responses;

namespace MedApp.Application.Demographics.Common;

/// <summary>Demographics lookups and the service's status, as the API returns them.</summary>
public static class DemographicsMapper
{
    public static DemographicsLookupResponse ToResponse(DemographicsRecord record, string source) => new()
    {
        NhsNumber = record.NhsNumber,
        FirstName = record.FirstName,
        LastName = record.LastName,
        DateOfBirth = record.DateOfBirth,
        Source = source
    };

    public static DemographicsStatusResponse ToStatus(IPersonalDemographicsService service) => new()
    {
        IsAvailable = service.IsAvailable,
        Source = service.IsAvailable ? service.Source : null
    };
}
