using FluentValidation;
using MedApp.Application.Authentication.Emails.Common;
using MedApp.Application.Common.Contacts;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Practices.Common;
using MedApp.Domain.Contacts;
using MedApp.Domain.Letters;

namespace MedApp.Application.Letters.Common;

/// <summary>A recipient as a request sends it, before it's checked.</summary>
public sealed record LetterRecipientCommandItem(
    string Kind,
    Guid? PracticeId,
    string? Name,
    string? AddressLines,
    string? Postcode,
    string? Email)
{
    /// <summary>What's typed for it that goes on the letter, for the font check.</summary>
    public string TypedText => string.Join("\n", Name, AddressLines, Postcode);

    public LetterRecipientInput ToInput() =>
        new(PathwayEnums.Parse<LetterRecipientKind>(Kind), PracticeId, Name, AddressLines, Postcode, Email);
}

/// <summary>The checks shared by letters' commands.</summary>
public static class LetterValidation
{
    public static string RecipientKindNames => string.Join(", ", Enum.GetNames<LetterRecipientKind>());

    public static IRuleBuilderOptions<T, int> ExpectedVersion<T>(this IRuleBuilder<T, int> rule) =>
        rule.GreaterThanOrEqualTo(1).WithMessage("Expected version must be the letter's version, 1 or more.");

    /// <summary>A comment or reason: required, at most <see cref="Letter.MaxCommentLength"/>.</summary>
    public static IRuleBuilderOptions<T, string> Comment<T>(this IRuleBuilder<T, string> rule, string label) =>
        rule
            .Must(text => !string.IsNullOrWhiteSpace(text))
            .WithMessage($"{label} is required.")
            .Must(text => text is null || text.Trim().Length <= Letter.MaxCommentLength)
            .WithMessage($"{label} must not exceed {Letter.MaxCommentLength} characters.");

    /// <summary>
    /// A draft's recipients: at most <see cref="Letter.MaxRecipients"/>; each of a known kind, a GP practice with its
    /// practice, another with a name and address, addresses and postcodes in shape, an email in an allowed domain, and
    /// typed text the letter font can show.
    /// </summary>
    public static void Recipients<T>(
        this AbstractValidator<T> validator,
        ILetterRenderer renderer,
        LettersOptions options,
        System.Linq.Expressions.Expression<Func<T, IEnumerable<LetterRecipientCommandItem>>> recipients)
    {
        validator.RuleFor(recipients)
            .Must(list => list.Count() <= Letter.MaxRecipients)
            .WithMessage($"A letter has at most {Letter.MaxRecipients} recipients.")
            .Must(list => list.Count(r => r.Kind == nameof(LetterRecipientKind.Patient)) <= 1)
            .WithMessage("The patient can be a recipient only once.");

        var domains = options.AllowedSecureEmailDomains;
        validator.RuleForEach(recipients).ChildRules(recipient =>
        {
            recipient.RuleFor(r => r.Kind)
                .Must(kind => PathwayEnums.TryParse<LetterRecipientKind>(kind, out _))
                .WithMessage($"Kind must be one of: {RecipientKindNames}.");

            recipient.RuleFor(r => r.PracticeId)
                .Must(id => id is not null && id != Guid.Empty)
                .When(r => r.Kind == nameof(LetterRecipientKind.GpPractice))
                .WithMessage("Choose the GP practice from the directory.");

            recipient.RuleFor(r => r.Name)
                .Must(name => !string.IsNullOrWhiteSpace(name))
                .When(r => r.Kind == nameof(LetterRecipientKind.Other))
                .WithMessage("Name is required for another recipient.")
                .Must(name => name is null || name.Trim().Length <= LetterRecipient.MaxNameLength)
                .WithMessage($"Name must not exceed {LetterRecipient.MaxNameLength} characters.");

            recipient.RuleFor(r => r.AddressLines)
                .Must(lines => !string.IsNullOrWhiteSpace(lines))
                .When(r => r.Kind == nameof(LetterRecipientKind.Other))
                .WithMessage("Address is required for another recipient.");

            recipient.RuleFor(r => (IReadOnlyList<string>?)Lines(r.AddressLines))
                .AddressLines()
                .OverridePropertyName("AddressLines");

            recipient.RuleFor(r => r.Postcode).Postcode();

            recipient.RuleFor(r => r.Email)
                .Must(email => string.IsNullOrWhiteSpace(email) || EmailAddressRules.Normalize(email) is not null)
                .WithMessage("Enter one email address, like name@nhs.net.")
                .Must(email => string.IsNullOrWhiteSpace(email)
                               || EmailAddressRules.Normalize(email) is null
                               || PracticeRules.IsInAllowedDomain(email, domains))
                .WithMessage(PracticeRules.SecureEmailDomainMessage(domains));

            recipient.RuleFor(r => r.TypedText)
                .Must(text => renderer.FindUnsupportedCharacters(text).Count == 0)
                .WithMessage((_, text) => LetterTemplateValidation.UnsupportedMessage(renderer.FindUnsupportedCharacters(text)))
                .OverridePropertyName("Name");
        });
    }

    private static IReadOnlyList<string> Lines(string? text) =>
        string.IsNullOrEmpty(text) ? [] : text.Replace("\r\n", "\n").Split('\n');
}
