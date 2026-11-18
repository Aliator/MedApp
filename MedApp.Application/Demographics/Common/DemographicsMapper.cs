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
        AddressLines = record.AddressLines ?? [],
        Postcode = record.Postcode,
        MobilePhone = record.MobilePhone,
        Source = source
    };

    public static DemographicsStatusResponse ToStatus(IPersonalDemographicsService service) => new()
    {
        IsAvailable = service.IsAvailable,
        Source = service.IsAvailable ? service.Source : null
    };
}
