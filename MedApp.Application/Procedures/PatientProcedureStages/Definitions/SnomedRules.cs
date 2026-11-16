using System.Linq.Expressions;
using FluentValidation;
using MedApp.Domain.Terminology;

namespace MedApp.Application.Procedures.PatientProcedureStages.Definitions;

/// <summary>
/// The SNOMED CT code and term on a stage definition: both or neither. The code must be a concept's SCTID
/// (<see cref="SnomedCt"/>); the term is the admin's own display text, as there's no terminology server to look it up.
/// </summary>
public static class SnomedRules
{
    public const string TermRequired = "Give the concept's display term with its SNOMED CT code.";
    public const string CodeRequired = "Give the SNOMED CT code with its display term.";
    public static readonly string TermTooLong = $"The display term must not exceed {SnomedCt.MaxTermLength} characters.";

    /// <summary>Adds the rules for a command's code and term, read by the given selectors.</summary>
    public static void AddSnomedRules<T>(
        this AbstractValidator<T> validator,
        Expression<Func<T, string?>> code,
        Expression<Func<T, string?>> term)
    {
        var codeOf = code.Compile();
        var termOf = term.Compile();

        validator.RuleFor(code)
            .Must(c => SnomedCt.IsConceptId(c!.Trim()))
            .WithMessage((_, c) => SnomedCt.WhyNotConceptId(c!.Trim()))
            .When(x => !string.IsNullOrWhiteSpace(codeOf(x)));

        validator.RuleFor(code)
            .Must(c => !string.IsNullOrWhiteSpace(c))
            .WithMessage(CodeRequired)
            .When(x => !string.IsNullOrWhiteSpace(termOf(x)));

        validator.RuleFor(term)
            .Must(t => !string.IsNullOrWhiteSpace(t))
            .WithMessage(TermRequired)
            .When(x => !string.IsNullOrWhiteSpace(codeOf(x)));

        validator.RuleFor(term)
            .Must(t => t is null || t.Trim().Length <= SnomedCt.MaxTermLength)
            .WithMessage(TermTooLong);
    }

    /// <summary>The code and term to keep: trimmed, or both null when there's no code.</summary>
    public static (string? Code, string? Term) Normalise(string? code, string? term) =>
        string.IsNullOrWhiteSpace(code) ? (null, null) : (code.Trim(), term?.Trim());
}
