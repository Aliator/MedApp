using System.Net.Mail;
using FluentValidation;

namespace MedApp.Application.Authentication.Emails.Common;

/// <summary>What a staff email address must be, wherever one is given (Account Settings, or an admin creating an account).</summary>
public static class EmailAddressRules
{
    /// <summary>Identity's email column.</summary>
    public const int MaxLength = 256;

    /// <summary>
    /// The bare address in <paramref name="value"/> (<c>name@example.org</c>, without a display name or angle brackets),
    /// which is all that's stored; null if it isn't one address, or is longer than <see cref="MaxLength"/>.
    /// </summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxLength)
            return null;

        // One address only: a list, or line breaks that could add headers, aren't one.
        if (value.IndexOfAny([',', ';', '\r', '\n']) >= 0)
            return null;

        return MailAddress.TryCreate(value.Trim(), out var address) && address.Address.Length <= MaxLength
            ? address.Address
            : null;
    }

    /// <summary>A plausible address of at most <see cref="MaxLength"/> characters; the confirmation link proves it's real.</summary>
    public static IRuleBuilderOptions<T, string?> ValidEmailAddress<T>(this IRuleBuilder<T, string?> rule) =>
        rule
            .MaximumLength(MaxLength)
            .WithMessage($"An email address can be at most {MaxLength} characters.")
            .Must(value => Normalize(value) is not null)
            .WithMessage("Enter one email address, like name@example.org.");
}
