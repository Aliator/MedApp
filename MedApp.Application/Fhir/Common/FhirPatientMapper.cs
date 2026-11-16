using System.Globalization;
using MedApp.Contracts.Fhir;
using MedApp.Domain.Patients;

namespace MedApp.Application.Fhir.Common;

/// <summary>
/// Patients as FHIR R4 resources in the UK Core Patient profile's shape: the NHS number as an identifier with its
/// verification status, the name, the email and the date of birth. Only what MedApp holds; nothing is made up.
/// </summary>
public static class FhirPatientMapper
{
    /// <summary>The UK Core Patient profile (UK Core STU3 / R4).</summary>
    public const string UkCorePatientProfile = "https://fhir.hl7.org.uk/StructureDefinition/UKCore-Patient";

    /// <summary>The identifier system for NHS numbers in England and Wales.</summary>
    public const string NhsNumberSystem = "https://fhir.nhs.uk/Id/nhs-number";

    public const string NhsNumberVerificationStatusExtension =
        "https://fhir.hl7.org.uk/StructureDefinition/Extension-UKCore-NHSNumberVerificationStatus";

    public const string NhsNumberVerificationStatusSystem =
        "https://fhir.hl7.org.uk/CodeSystem/UKCore-NHSNumberVerificationStatusEngland";

    /// <summary>
    /// "Number present but not traced": MedApp checks the number's check digit, but doesn't trace it against PDS, so it
    /// never claims "01" (present and verified).
    /// </summary>
    public const string NotTracedCode = "02";

    public const string NotTracedDisplay = "Number present but not traced";

    public static FhirPatient ToResource(Patient patient) => new()
    {
        Id = patient.Id.ToString(),
        Meta = new FhirMeta
        {
            LastUpdated = Instant(patient.LastUpdated),
            Profile = [UkCorePatientProfile]
        },
        Identifier = patient.NhsNumber is { } nhsNumber
            ?
            [
                new FhirIdentifier
                {
                    Extension =
                    [
                        new FhirExtension
                        {
                            Url = NhsNumberVerificationStatusExtension,
                            ValueCodeableConcept = new FhirCodeableConcept
                            {
                                Coding =
                                [
                                    new FhirCoding
                                    {
                                        System = NhsNumberVerificationStatusSystem,
                                        Code = NotTracedCode,
                                        Display = NotTracedDisplay
                                    }
                                ]
                            }
                        }
                    ],
                    System = NhsNumberSystem,
                    Value = nhsNumber
                }
            ]
            : null,
        Name =
        [
            new FhirHumanName
            {
                Use = "official",
                Family = NullIfEmpty(patient.LastName),
                Given = string.IsNullOrWhiteSpace(patient.FirstName) ? null : [patient.FirstName]
            }
        ],
        Telecom = string.IsNullOrWhiteSpace(patient.Email)
            ? null
            : [new FhirContactPoint { System = "email", Value = patient.Email }],
        BirthDate = patient.DateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
    };

    /// <summary>A searchset Bundle of the patients found, each with its absolute address under <paramref name="baseUrl"/>.</summary>
    public static FhirBundle ToSearchSet(IReadOnlyList<Patient> patients, string baseUrl, string selfUrl, DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Meta = new FhirMeta { LastUpdated = Instant(nowUtc) },
        Type = "searchset",
        Timestamp = Instant(nowUtc),
        Total = patients.Count,
        Link = [new FhirBundleLink { Relation = "self", Url = selfUrl }],
        Entry = patients.Count == 0
            ? null
            : patients
                .Select(p => new FhirBundleEntry
                {
                    FullUrl = $"{baseUrl.TrimEnd('/')}/Patient/{p.Id}",
                    Resource = ToResource(p),
                    Search = new FhirBundleEntrySearch { Mode = "match" }
                })
                .ToList()
    };

    /// <summary>A FHIR instant: UTC to the millisecond, with a Z.</summary>
    public static string Instant(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    private static string? NullIfEmpty(string text) => string.IsNullOrWhiteSpace(text) ? null : text;
}
