using FluentValidation;
using MedApp.Domain.Patients;

namespace MedApp.Application.Patients.Common;

/// <summary>The NHS number's validation, shared by the commands that take one.</summary>
public static class NhsNumberRules
{
    public const string NotTenDigits = "NHS number must be 10 digits.";

    /// <summary>The 409 when another patient has the number: it's unique.</summary>
    public const string InUse = "Another patient already has this NHS number. Check you have the right patient.";

    public const string WrongCheckDigit =
        "This isn't a valid NHS number: its check digit doesn't match. Check it was typed correctly.";

    /// <summary>
    /// An optional NHS number: nothing (null, empty or spaces), or ten digits (spaces allowed anywhere) whose last is
    /// the right Modulus 11 check digit (<see cref="NhsNumbers"/>). At most one message: the length, or the check digit.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> NhsNumber<T>(this IRuleBuilder<T, string?> rule) =>
        rule
            .Must(number => string.IsNullOrWhiteSpace(number) || NhsNumbers.IsTenDigits(number))
            .WithMessage(NotTenDigits)
            .Must(number => !NhsNumbers.IsTenDigits(number) || NhsNumbers.IsValid(number))
            .WithMessage(WrongCheckDigit);
}
