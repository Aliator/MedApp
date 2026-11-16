using System.Globalization;
using MedApp.Contracts.Fhir;
using MedApp.Domain.Patients;

namespace MedApp.Application.Fhir.Common;

/// <summary>
/// Patients as FHIR R4 resources in the UK Core Patient profile's shape: the NHS number as an identifier with its
/// verification status, the name, the email and the date of birth. Only what MedApp holds, and an anonymised record's
/// placeholders are never passed off as a patient's details.
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

    /// <summary>
    /// MedApp's own code system for flags on a record (<c>meta.tag</c>): <see cref="RestrictedCode"/> and
    /// <see cref="DisputedCode"/>. A local URN until MedApp publishes a canonical URL for it.
    /// </summary>
    public const string RecordFlagSystem = "urn:medapp:fhir:CodeSystem:record-flag";

    public const string RestrictedCode = "restricted";
    public const string RestrictedDisplay = "Processing restricted (UK GDPR Article 18): the record can't be changed";
    public const string DisputedCode = "disputed";
    public const string DisputedDisplay = "Accuracy disputed by the patient (UK GDPR Article 16)";

    /// <summary>What an anonymised record's name says, as FHIR's "anonymous" name.</summary>
    public const string AnonymisedName = "Anonymised";

    /// <summary>
    /// The patient's resource. An anonymised record says so in FHIR's own terms, rather than passing its placeholders off
    /// as details: <c>active</c> false, an "anonymous" name, the year of birth alone (the day and month the anonymiser
    /// set aren't real), and no email. A restricted or disputed record carries a <c>meta.tag</c> saying so.
    /// </summary>
    public static FhirPatient ToResource(Patient patient) =>
        patient.AnonymisedAtUtc is not null ? ToAnonymisedResource(patient) : new()
        {
            Id = patient.Id.ToString(),
            Meta = Meta(patient),
            Identifier = patient.NhsNumber is { } nhsNumber ? [NhsNumberIdentifier(nhsNumber)] : null,
            Active = true,
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

    private static FhirPatient ToAnonymisedResource(Patient patient) => new()
    {
        Id = patient.Id.ToString(),
        Meta = Meta(patient),
        Active = false,
        Name = [new FhirHumanName { Use = "anonymous", Text = AnonymisedName }],
        BirthDate = patient.DateOfBirth.Year.ToString("0000", CultureInfo.InvariantCulture)
    };

    private static FhirMeta Meta(Patient patient)
    {
        var tags = new List<FhirCoding>();
        if (patient.RestrictedAtUtc is not null)
            tags.Add(new FhirCoding { System = RecordFlagSystem, Code = RestrictedCode, Display = RestrictedDisplay });
        if (patient.DisputedAtUtc is not null)
            tags.Add(new FhirCoding { System = RecordFlagSystem, Code = DisputedCode, Display = DisputedDisplay });

        return new FhirMeta
        {
            LastUpdated = Instant(patient.LastUpdated),
            Profile = [UkCorePatientProfile],
            Tag = tags.Count > 0 ? tags : null
        };
    }

    private static FhirIdentifier NhsNumberIdentifier(string nhsNumber) => new()
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
