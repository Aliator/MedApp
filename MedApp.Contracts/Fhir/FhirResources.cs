using System.Text.Json.Serialization;

namespace MedApp.Contracts.Fhir;

// The few FHIR R4 resources and data types MedApp's read-only FHIR API returns, hand-built rather than taken from a FHIR
// library, as the API needs only these. They follow the FHIR R4 JSON format (https://hl7.org/fhir/R4/json.html): send
// them with FhirJson's options, which leave out nulls, since FHIR JSON never holds a null or an empty list. Lists are
// therefore null rather than empty when there's nothing in them.

/// <summary>A FHIR resource: what every resource's JSON starts with.</summary>
public abstract class FhirResource(string resourceType)
{
    /// <summary>The resource's type, e.g. "Patient". First in the JSON, as FHIR expects.</summary>
    [JsonPropertyOrder(-2)]
    public string ResourceType { get; } = resourceType;

    [JsonPropertyOrder(-1)]
    public string? Id { get; set; }

    public FhirMeta? Meta { get; set; }
}

public sealed class FhirMeta
{
    /// <summary>When the resource last changed, as a FHIR instant (UTC, with a Z).</summary>
    public string? LastUpdated { get; set; }

    /// <summary>The profiles it claims to meet, e.g. UK Core Patient.</summary>
    public List<string>? Profile { get; set; }

    /// <summary>Tags about the record, e.g. that it's restricted or disputed (MedApp's record-flag code system).</summary>
    public List<FhirCoding>? Tag { get; set; }
}

/// <summary>
/// A patient, in the shape of the UK Core Patient profile
/// (<c>https://fhir.hl7.org.uk/StructureDefinition/UKCore-Patient</c>).
/// </summary>
public sealed class FhirPatient() : FhirResource("Patient")
{
    public List<FhirIdentifier>? Identifier { get; set; }

    /// <summary>Whether the record is in active use: false once it's anonymised.</summary>
    public bool? Active { get; set; }

    public List<FhirHumanName>? Name { get; set; }

    public List<FhirContactPoint>? Telecom { get; set; }

    /// <summary>The date of birth, as a FHIR date: yyyy-MM-dd, or the year alone (yyyy) when that's all that's known.</summary>
    public string? BirthDate { get; set; }
}

public sealed class FhirIdentifier
{
    public List<FhirExtension>? Extension { get; set; }
    public string? System { get; set; }
    public string? Value { get; set; }
}

public sealed class FhirExtension
{
    public string Url { get; set; } = string.Empty;
    public FhirCodeableConcept? ValueCodeableConcept { get; set; }
}

public sealed class FhirCodeableConcept
{
    public List<FhirCoding>? Coding { get; set; }
}

public sealed class FhirCoding
{
    public string? System { get; set; }
    public string? Code { get; set; }
    public string? Display { get; set; }
}

public sealed class FhirHumanName
{
    /// <summary>"official" for the name the patient is registered under; "anonymous" for an anonymised record's.</summary>
    public string? Use { get; set; }

    /// <summary>The whole name as text, e.g. "Anonymised".</summary>
    public string? Text { get; set; }

    public string? Family { get; set; }
    public List<string>? Given { get; set; }
}

public sealed class FhirContactPoint
{
    /// <summary>"email", "phone" and so on.</summary>
    public string? System { get; set; }

    public string? Value { get; set; }
}

/// <summary>A searchset Bundle: a search's results.</summary>
public sealed class FhirBundle() : FhirResource("Bundle")
{
    /// <summary>"searchset" for a search's results.</summary>
    public string Type { get; set; } = "searchset";

    /// <summary>When the bundle was made, as a FHIR instant.</summary>
    public string? Timestamp { get; set; }

    /// <summary>How many resources matched.</summary>
    public int Total { get; set; }

    public List<FhirBundleLink>? Link { get; set; }

    public List<FhirBundleEntry>? Entry { get; set; }
}

public sealed class FhirBundleLink
{
    /// <summary>"self" for the search that made the bundle.</summary>
    public string Relation { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;
}

public sealed class FhirBundleEntry
{
    /// <summary>The resource's absolute address.</summary>
    public string? FullUrl { get; set; }

    public FhirPatient? Resource { get; set; }

    public FhirBundleEntrySearch? Search { get; set; }
}

public sealed class FhirBundleEntrySearch
{
    /// <summary>"match" for a resource that matched the search.</summary>
    public string Mode { get; set; } = "match";
}

/// <summary>What went wrong with a FHIR request: FHIR's error response, in place of ProblemDetails.</summary>
public sealed class FhirOperationOutcome() : FhirResource("OperationOutcome")
{
    public List<FhirOperationOutcomeIssue> Issue { get; set; } = [];
}

public sealed class FhirOperationOutcomeIssue
{
    /// <summary>"fatal", "error", "warning" or "information".</summary>
    public string Severity { get; set; } = "error";

    /// <summary>
    /// The FHIR issue type, e.g. "not-found", "invalid", "required", "login", "forbidden" or "exception"
    /// (<c>http://hl7.org/fhir/issue-type</c>).
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>What went wrong, in words.</summary>
    public string? Diagnostics { get; set; }

    /// <summary>Where, as FHIRPath or a parameter name, e.g. "identifier".</summary>
    public List<string>? Expression { get; set; }
}

/// <summary>
/// What the FHIR API can do (<c>GET api/fhir/metadata</c>), which FHIR clients read first. Only the parts MedApp's
/// small API needs.
/// </summary>
public sealed class FhirCapabilityStatement() : FhirResource("CapabilityStatement")
{
    /// <summary>"active".</summary>
    public string Status { get; set; } = "active";

    /// <summary>When it was published, as a FHIR dateTime.</summary>
    public string? Date { get; set; }

    /// <summary>"instance": this server, as deployed.</summary>
    public string Kind { get; set; } = "instance";

    public FhirCapabilitySoftware? Software { get; set; }

    /// <summary>"4.0.1" (FHIR R4).</summary>
    public string FhirVersion { get; set; } = "4.0.1";

    /// <summary>The formats it answers in: JSON only.</summary>
    public List<string> Format { get; set; } = [];

    public List<FhirCapabilityRest> Rest { get; set; } = [];
}

public sealed class FhirCapabilitySoftware
{
    public string Name { get; set; } = string.Empty;
}

public sealed class FhirCapabilityRest
{
    /// <summary>"server".</summary>
    public string Mode { get; set; } = "server";

    public List<FhirCapabilityResource> Resource { get; set; } = [];
}

public sealed class FhirCapabilityResource
{
    public string Type { get; set; } = string.Empty;

    /// <summary>The profile its resources follow.</summary>
    public string? Profile { get; set; }

    public List<FhirCapabilityInteraction> Interaction { get; set; } = [];

    public List<FhirCapabilitySearchParam>? SearchParam { get; set; }
}

public sealed class FhirCapabilityInteraction
{
    /// <summary>"read", "search-type" and so on.</summary>
    public string Code { get; set; } = string.Empty;
}

public sealed class FhirCapabilitySearchParam
{
    public string Name { get; set; } = string.Empty;

    /// <summary>"token" and so on.</summary>
    public string Type { get; set; } = string.Empty;

    public string? Documentation { get; set; }
}
