using FluentValidation;
using MedApp.Domain.Contacts;

namespace MedApp.Application.Common.Contacts;

/// <summary>
/// The validation of contact details (a postal address's lines, a postcode, phone numbers), shared by patients and the
/// GP practice directory. Formats only: nothing checks that an address, postcode or number exists.
/// </summary>
public static class ContactRules
{
    public static readonly string TooManyLines = $"An address has at most {PostalAddress.MaxLines} lines (the postcode has its own field).";

    public static readonly string LineTooLong = $"Each address line must not exceed {PostalAddress.MaxLineLength} characters.";

    public const string NotAPostcode = "Postcode must be a UK postcode, such as ZZ1 1ZZ.";

    public const string NotAMobile = "Mobile must be a UK mobile number: 07 and nine more digits, or +44 7 and nine more.";

    public const string NotAPhoneNumber = "Phone must be a UK phone number: 0 and nine or ten more digits, or +44 and the same.";

    /// <summary>Optional address lines: at most <see cref="PostalAddress.MaxLines"/> once empty ones are dropped, each short enough.</summary>
    public static IRuleBuilderOptions<T, IReadOnlyList<string>?> AddressLines<T>(this IRuleBuilder<T, IReadOnlyList<string>?> rule) =>
        rule
            .Must(lines => PostalAddress.Tidy(lines).Count <= PostalAddress.MaxLines)
            .WithMessage(TooManyLines)
            .Must(lines => PostalAddress.Tidy(lines).All(line => line.Length <= PostalAddress.MaxLineLength))
            .WithMessage(LineTooLong);

    /// <summary>An optional UK postcode, as typed.</summary>
    public static IRuleBuilderOptions<T, string?> Postcode<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(UkPostcodes.IsValidOrEmpty).WithMessage(NotAPostcode);

    /// <summary>An optional UK mobile, as typed.</summary>
    public static IRuleBuilderOptions<T, string?> MobilePhone<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(UkPhoneNumbers.IsMobileOrEmpty).WithMessage(NotAMobile);

    /// <summary>An optional UK phone number (landline or mobile), as typed.</summary>
    public static IRuleBuilderOptions<T, string?> PhoneNumber<T>(this IRuleBuilder<T, string?> rule) =>
        rule
            .Must(number => number is null || number.Trim().Length <= UkPhoneNumbers.MaxPhoneLength)
            .WithMessage($"Phone must not exceed {UkPhoneNumbers.MaxPhoneLength} characters.")
            // One message at a time: too long, or (once short enough) not a UK number.
            .Must(number => number is null || number.Trim().Length > UkPhoneNumbers.MaxPhoneLength
                            || UkPhoneNumbers.IsUkNumberOrEmpty(number))
            .WithMessage(NotAPhoneNumber);
}
