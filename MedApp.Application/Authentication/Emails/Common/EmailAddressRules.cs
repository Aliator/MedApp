using FluentValidation;

namespace MedApp.Application.Authentication.Emails.Common;

/// <summary>What a staff email address must be, wherever one is given (Account Settings, or an admin creating an account).</summary>
public static class EmailAddressRules
{
    /// <summary>Identity's email column.</summary>
    public const int MaxLength = 256;

    /// <summary>A plausible address of at most <see cref="MaxLength"/> characters; the confirmation link proves it's real.</summary>
    public static IRuleBuilderOptions<T, string?> ValidEmailAddress<T>(this IRuleBuilder<T, string?> rule) =>
        rule
            .MaximumLength(MaxLength)
            .WithMessage($"An email address can be at most {MaxLength} characters.")
            .EmailAddress()
            .WithMessage("Enter an email address, like name@example.org.");
}
