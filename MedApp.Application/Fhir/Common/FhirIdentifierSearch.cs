namespace MedApp.Application.Fhir.Common;

/// <summary>
/// A FHIR token search parameter, <c>[system]|[value]</c> or just <c>[value]</c>
/// (https://hl7.org/fhir/R4/search.html#token), as the Patient search's <c>identifier</c> takes it.
/// </summary>
public sealed record FhirIdentifierSearch(string? System, string Value)
{
    /// <summary>
    /// The token's parts. Without a <c>|</c>, the value is matched in any system; with one, only in that system (an
    /// empty system means "no system", which no MedApp identifier has).
    /// </summary>
    public static FhirIdentifierSearch Parse(string token)
    {
        var bar = token.IndexOf('|');
        return bar < 0
            ? new FhirIdentifierSearch(null, token.Trim())
            : new FhirIdentifierSearch(token[..bar].Trim(), token[(bar + 1)..].Trim());
    }

    /// <summary>Whether the search could match an NHS number: no system given, or the NHS number's.</summary>
    public bool IsForNhsNumber => System is null || System == FhirPatientMapper.NhsNumberSystem;
}
