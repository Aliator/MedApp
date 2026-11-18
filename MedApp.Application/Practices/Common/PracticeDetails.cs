using MedApp.Application.Authentication.Emails.Common;
using MedApp.Application.Referrals.Common;
using MedApp.Domain.Contacts;

namespace MedApp.Application.Practices.Common;

/// <summary>
/// A practice's details as typed, tidied: the name trimmed, the ODS code without spaces in upper case, the address's
/// lines as one stored value, the postcode normalised, the phone tidied and the email bare. Adding and editing take the
/// same details.
/// </summary>
public sealed record PracticeDetails(
    string Name,
    string? OdsCode,
    string? AddressLines,
    string? Postcode,
    string? Phone,
    string? SecureEmail)
{
    /// <summary>The details as typed, tidied. The validator has checked each.</summary>
    public static PracticeDetails From(
        string name,
        string? odsCode,
        IReadOnlyList<string>? addressLines,
        string? postcode,
        string? phone,
        string? secureEmail) => new(
        name.Trim(),
        OdsCodes.Normalise(odsCode),
        PostalAddress.Join(addressLines),
        UkPostcodes.Normalise(postcode),
        UkPhoneNumbers.Tidy(phone),
        string.IsNullOrWhiteSpace(secureEmail) ? null : EmailAddressRules.Normalize(secureEmail));

    /// <summary>Whether a practice already has exactly these details.</summary>
    public bool Matches(Domain.Practices.Practice practice) =>
        practice.Name == Name
        && practice.OdsCode == OdsCode
        && practice.AddressLines == AddressLines
        && practice.Postcode == Postcode
        && practice.Phone == Phone
        && practice.SecureEmail == SecureEmail;

    /// <summary>Copies the details onto a practice.</summary>
    public void ApplyTo(Domain.Practices.Practice practice)
    {
        practice.Name = Name;
        practice.OdsCode = OdsCode;
        practice.AddressLines = AddressLines;
        practice.Postcode = Postcode;
        practice.Phone = Phone;
        practice.SecureEmail = SecureEmail;
    }
}
