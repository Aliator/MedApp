using FluentValidation;
using MedApp.Application.Common.Contacts;
using MedApp.Contracts.Patients.Requests;
using MedApp.Contracts.Patients.Responses;
using MedApp.Domain.Contacts;
using MedApp.Domain.Patients;
using MedApp.Domain.Practices;

namespace MedApp.Application.Patients.Common;

/// <summary>A GP practice that isn't in the directory, as a command carries it.</summary>
public sealed record UnlistedPracticeInput(string Name, IReadOnlyList<string>? AddressLines, string? Postcode, string? Phone)
{
    public static UnlistedPracticeInput? From(UnlistedPracticeRequest? request) =>
        request is null ? null : new(request.Name, request.AddressLines, request.Postcode, request.Phone);
}

/// <summary>
/// A patient's GP practice when it isn't in the directory ("Not listed here"): its name, address and phone, held on
/// the patient only and never added to the directory. A patient has a directory practice or an unlisted one, never both.
/// </summary>
public static class UnlistedPractices
{
    public const string BothMessage =
        "Choose a practice from the directory or enter one that isn't listed, not both.";

    public static bool Has(Patient patient) => patient.UnlistedPracticeName is not null;

    /// <summary>The input's values as the patient stores them: trimmed, the address as one value, the postcode normalised.</summary>
    public static (string Name, string? AddressLines, string? Postcode, string? Phone) Normalise(UnlistedPracticeInput input) =>
        (input.Name.Trim(), PostalAddress.Join(input.AddressLines), UkPostcodes.Normalise(input.Postcode), UkPhoneNumbers.Tidy(input.Phone));

    /// <summary>Gives the patient this unlisted practice; true when anything changed.</summary>
    public static bool Set(Patient patient, UnlistedPracticeInput input)
    {
        var (name, lines, postcode, phone) = Normalise(input);
        if (name == patient.UnlistedPracticeName && lines == patient.UnlistedPracticeAddressLines
            && postcode == patient.UnlistedPracticePostcode && phone == patient.UnlistedPracticePhone)
            return false;

        patient.UnlistedPracticeName = name;
        patient.UnlistedPracticeAddressLines = lines;
        patient.UnlistedPracticePostcode = postcode;
        patient.UnlistedPracticePhone = phone;
        return true;
    }

    /// <summary>Removes the patient's unlisted practice; true when they had one.</summary>
    public static bool Clear(Patient patient)
    {
        var had = Has(patient);
        patient.UnlistedPracticeName = null;
        patient.UnlistedPracticeAddressLines = null;
        patient.UnlistedPracticePostcode = null;
        patient.UnlistedPracticePhone = null;
        return had;
    }

    public static UnlistedPracticeResponse? ToResponse(Patient patient) => Has(patient)
        ? new UnlistedPracticeResponse
        {
            Name = patient.UnlistedPracticeName!,
            AddressLines = PostalAddress.Split(patient.UnlistedPracticeAddressLines),
            Postcode = patient.UnlistedPracticePostcode,
            Phone = patient.UnlistedPracticePhone
        }
        : null;

    /// <summary>
    /// The checks an unlisted practice gets: a name (at most 200 characters), the address, postcode and phone in shape.
    /// Each is named <c>UnlistedPractice…</c> (after the parent's prefix), so a form tells them from the patient's own
    /// address and postcode.
    /// </summary>
    public static IRuleBuilderOptions<T, UnlistedPracticeInput?> UnlistedPractice<T>(this IRuleBuilder<T, UnlistedPracticeInput?> rule) =>
        rule.ChildRules(practice =>
        {
            practice.RuleFor(p => p!.Name)
                .Must(name => !string.IsNullOrWhiteSpace(name))
                .WithMessage("Enter the practice's name.")
                .Must(name => name is null || name.Trim().Length <= Practice.MaxNameLength)
                .WithMessage($"The practice's name must not exceed {Practice.MaxNameLength} characters.")
                .OverridePropertyName("UnlistedPracticeName");
            practice.RuleFor(p => p!.AddressLines).AddressLines().OverridePropertyName("UnlistedPracticeAddressLines");
            practice.RuleFor(p => p!.Postcode).Postcode().OverridePropertyName("UnlistedPracticePostcode");
            practice.RuleFor(p => p!.Phone).PhoneNumber().OverridePropertyName("UnlistedPracticePhone");
        });
}
