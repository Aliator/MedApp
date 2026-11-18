using FluentValidation;
using MedApp.Application.Authentication.Emails.Common;
using MedApp.Application.Common.Contacts;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Practices.Repositories;
using MedApp.Application.Referrals.Common;
using MedApp.Domain.Practices;

namespace MedApp.Application.Practices.Common;

/// <summary>The rules and messages for the GP practice directory, shared by its handlers and those that choose a practice.</summary>
public static class PracticeRules
{
    public const string ChangedMeanwhileMessage =
        "Someone changed this practice while you had it open. Reload to see the change, then try again.";

    public const string JustUsedMessage =
        "This practice has just been given to a patient or referral, so it can't be deleted. Archive it instead.";

    public static string OdsCodeTakenMessage(string odsCode) =>
        $"Another practice in the directory already has the ODS code {odsCode}.";

    public static string InUseMessage(Practice practice, PracticeUsage usage) =>
        $"{practice.Name} has {Describe(usage)}, so it can't be deleted. Archive it instead.";

    public static string ArchivedMessage(Practice practice) =>
        $"{practice.Name} is archived, so it can't be chosen. Restore it on the Practices page first.";

    public static string SecureEmailDomainMessage(IReadOnlyList<string> domains) =>
        $"A secure email address must be in {string.Join(" or ", domains)}.";

    public static async Task<Practice> GetOrThrowAsync(this IPracticeRepository repository, Guid id, CancellationToken ct) =>
        await repository.GetAsync(id, ct) ?? throw new NotFoundException($"Practice '{id}' was not found.");

    /// <summary>A change must start from the version saved now, or someone else has changed it since (409).</summary>
    public static void EnsureVersion(Practice practice, int expectedVersion)
    {
        if (practice.Version != expectedVersion)
            throw new ConflictException(ChangedMeanwhileMessage);
    }

    /// <summary>
    /// The practice being given to a patient or referral: it must exist (404), and an archived one may only stay where
    /// it's already given (<paramref name="current"/>; 409).
    /// </summary>
    public static async Task<Practice> CheckChosenAsync(
        IPracticeRepository repository, Guid practiceId, Guid? current, CancellationToken ct)
    {
        var practice = await repository.GetOrThrowAsync(practiceId, ct);
        if (practice.IsArchived && practice.Id != current)
            throw new ConflictException(ArchivedMessage(practice));
        return practice;
    }

    /// <summary>Whether an address is in one of the allowed domains, or a subdomain of one (ignoring case).</summary>
    public static bool IsInAllowedDomain(string? email, IReadOnlyList<string> domains)
    {
        var address = EmailAddressRules.Normalize(email);
        if (address is null)
            return false;
        var domain = address[(address.LastIndexOf('@') + 1)..].ToLowerInvariant();
        return domains.Any(allowed => domain == allowed || domain.EndsWith("." + allowed, StringComparison.Ordinal));
    }

    /// <summary>
    /// The checks a practice's details get, wherever they're typed: the name, the ODS code's format, the address, the
    /// postcode, the phone and the secure email's domain.
    /// </summary>
    public static void PracticeDetails<T>(
        this AbstractValidator<T> validator,
        LettersOptions options,
        System.Linq.Expressions.Expression<Func<T, string>> name,
        System.Linq.Expressions.Expression<Func<T, string?>> odsCode,
        System.Linq.Expressions.Expression<Func<T, IReadOnlyList<string>?>> addressLines,
        System.Linq.Expressions.Expression<Func<T, string?>> postcode,
        System.Linq.Expressions.Expression<Func<T, string?>> phone,
        System.Linq.Expressions.Expression<Func<T, string?>> secureEmail)
    {
        validator.RuleFor(name)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Name is required.")
            .Must(value => value is null || value.Trim().Length <= Practice.MaxNameLength)
            .WithMessage($"Name must not exceed {Practice.MaxNameLength} characters.");

        validator.RuleFor(odsCode)
            .Must(OdsCodes.IsValidOrEmpty)
            .WithMessage($"ODS code must be {OdsCodes.MinLength} to {OdsCodes.MaxLength} letters and digits.");

        validator.RuleFor(addressLines).AddressLines();
        validator.RuleFor(postcode).Postcode();
        validator.RuleFor(phone).PhoneNumber();

        var domains = options.AllowedSecureEmailDomains;
        validator.RuleFor(secureEmail)
            .Must(email => string.IsNullOrWhiteSpace(email) || EmailAddressRules.Normalize(email) is not null)
            .WithMessage("Enter one email address, like name@nhs.net.")
            .Must(email => string.IsNullOrWhiteSpace(email)
                           || EmailAddressRules.Normalize(email) is null
                           || IsInAllowedDomain(email, domains))
            .WithMessage(SecureEmailDomainMessage(domains));
    }

    private static string Describe(PracticeUsage usage)
    {
        var parts = new List<string>();
        if (usage.PatientCount > 0)
            parts.Add(Count(usage.PatientCount, "patient", "patients"));
        if (usage.ReferralCount > 0)
            parts.Add(Count(usage.ReferralCount, "referral", "referrals"));
        return string.Join(" and ", parts);
    }

    private static string Count(int count, string one, string many) => count == 1 ? $"1 {one}" : $"{count} {many}";
}
