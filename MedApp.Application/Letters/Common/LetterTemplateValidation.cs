using System.Linq.Expressions;
using FluentValidation;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Letters;

namespace MedApp.Application.Letters.Common;

/// <summary>The checks a letter template's text gets, wherever it's sent: saving it, or previewing it.</summary>
public static class LetterTemplateValidation
{
    public static string UnsupportedMessage(IReadOnlyList<string> characters) =>
        $"The letter font can't show {string.Join(" ", characters)}. Use other characters, or leave them out.";

    /// <summary>The name and kind of a template being saved.</summary>
    public static void NameAndKind<T>(
        this AbstractValidator<T> validator, Expression<Func<T, string>> name, Expression<Func<T, string>> kind)
    {
        validator.RuleFor(name)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Name is required.")
            .Must(value => value is null || value.Trim().Length <= LetterTemplate.MaxNameLength)
            .WithMessage($"Name must not exceed {LetterTemplate.MaxNameLength} characters.");

        validator.RuleFor(kind)
            .Must(value => PathwayEnums.TryParse<LetterKind>(value, out _))
            .WithMessage($"Kind must be one of: {LetterKinds.Names}.");
    }

    /// <summary>
    /// The letterhead, body skeleton and closing: within their lengths, with only known merge fields, and only
    /// characters the letter font can show.
    /// </summary>
    public static void TemplateText<T>(
        this AbstractValidator<T> validator,
        ILetterRenderer renderer,
        Expression<Func<T, string>> letterhead,
        Expression<Func<T, string>> bodySkeleton,
        Expression<Func<T, string>> closing)
    {
        validator.Text(renderer, letterhead, "Letterhead", LetterTemplate.MaxLetterheadLength);
        validator.Text(renderer, bodySkeleton, "Body skeleton", LetterTemplate.MaxBodyLength);
        validator.Text(renderer, closing, "Closing", LetterTemplate.MaxClosingLength);
    }

    private static void Text<T>(
        this AbstractValidator<T> validator,
        ILetterRenderer renderer,
        Expression<Func<T, string>> text,
        string label,
        int maxLength)
    {
        validator.RuleFor(text)
            .Must(value => LetterTemplateRules.Tidy(value).Length <= maxLength)
            .WithMessage($"{label} must not exceed {maxLength:N0} characters.")
            .Custom((value, context) =>
            {
                if (LetterMergeFields.UnknownMessage(value) is { } unknown)
                    context.AddFailure(unknown);

                var unsupported = renderer.FindUnsupportedCharacters(value);
                if (unsupported.Count > 0)
                    context.AddFailure(UnsupportedMessage(unsupported));
            });
    }
}
