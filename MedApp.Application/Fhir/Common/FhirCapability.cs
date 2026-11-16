using MedApp.Contracts.Fhir;

namespace MedApp.Application.Fhir.Common;

/// <summary>
/// The FHIR API's CapabilityStatement: FHIR R4, JSON only, and one resource, Patient, which can be read and searched
/// by identifier, in the UK Core profile's shape.
/// </summary>
public static class FhirCapability
{
    public const string FhirVersion = "4.0.1";

    public static FhirCapabilityStatement Statement(DateTime nowUtc) => new()
    {
        Status = "active",
        Date = FhirPatientMapper.Instant(nowUtc),
        Kind = "instance",
        Software = new FhirCapabilitySoftware { Name = "MedApp" },
        FhirVersion = FhirVersion,
        Format = ["json"],
        Rest =
        [
            new FhirCapabilityRest
            {
                Mode = "server",
                Resource =
                [
                    new FhirCapabilityResource
                    {
                        Type = "Patient",
                        Profile = FhirPatientMapper.UkCorePatientProfile,
                        Interaction = [new FhirCapabilityInteraction { Code = "read" }, new FhirCapabilityInteraction { Code = "search-type" }],
                        SearchParam =
                        [
                            new FhirCapabilitySearchParam
                            {
                                Name = "identifier",
                                Type = "token",
                                Documentation = $"The NHS number: {FhirPatientMapper.NhsNumberSystem}|{{number}}. Other systems match nothing."
                            }
                        ]
                    }
                ]
            }
        ]
    };
}
