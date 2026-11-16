using MedApp.Domain.Patients;

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

    /// <summary>Whether the search names the NHS number's system, so its value must be a valid NHS number.</summary>
    public bool NamesNhsNumberSystem => System == FhirPatientMapper.NhsNumberSystem;

    /// <summary>
    /// The NHS number (ten digits) to look for, or null when the search can't match one. With the NHS number's system,
    /// the value (already validated). With no system, the value matches in any system, so it's looked for as an NHS
    /// number only when it is one (ten digits, the right check digit, not a placeholder); anything else matches
    /// nothing, as MedApp has no other identifiers. With another system, nothing.
    /// </summary>
    public string? NhsNumberToMatch => System switch
    {
        FhirPatientMapper.NhsNumberSystem => NhsNumbers.Normalise(Value),
        null when NhsNumbers.IsValid(Value) && !NhsNumbers.IsPlaceholder(Value) => NhsNumbers.Normalise(Value),
        _ => null
    };
}
