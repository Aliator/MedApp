using FluentValidation;
using MedApp.Domain.Specimens;

namespace MedApp.Application.Specimens.Common;

/// <summary>Validation rules shared by the specimen commands.</summary>
public static class SpecimenRules
{
    public const string RequestingUserMessage = "Choose the clinician who asked for the specimen.";

    public static string? Optional(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    public static IRuleBuilderOptions<T, string?> Site<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(Specimen.MaxSiteLength).WithMessage($"The site must be at most {Specimen.MaxSiteLength} characters.");

    public static IRuleBuilderOptions<T, string?> RequestNotes<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(Specimen.MaxRequestNotesLength)
            .WithMessage($"The request notes must be at most {Specimen.MaxRequestNotesLength:N0} characters.");

    public static IRuleBuilderOptions<T, string?> Reference<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(Specimen.MaxReferenceLength)
            .WithMessage($"The reference must be at most {Specimen.MaxReferenceLength} characters.");

    /// <summary>A detail that must be given (lost, entered in error), at most 500 characters.</summary>
    public static IRuleBuilderOptions<T, string?> RequiredDetail<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(d => !string.IsNullOrWhiteSpace(d)).WithMessage("Say what happened.")
            .MaximumLength(Specimen.MaxClosedDetailLength)
            .WithMessage($"The detail must be at most {Specimen.MaxClosedDetailLength} characters.");

    /// <summary>
    /// The step's time: <paramref name="atUtc"/>, or now when it's null; a 400 on <paramref name="field"/> unless it's
    /// allowed (<see cref="SpecimenWorkflow.WhyTimeInvalid"/>).
    /// </summary>
    public static DateTime StepTime(DateTime? atUtc, DateTime? previousUtc, DateTime nowUtc, string step, string field)
    {
        var at = atUtc is not { } given ? nowUtc
            : given.Kind == DateTimeKind.Local ? given.ToUniversalTime()
            : DateTime.SpecifyKind(given, DateTimeKind.Utc);
        if (SpecimenWorkflow.WhyTimeInvalid(at, previousUtc, nowUtc, step) is { } why)
            throw new ValidationException([new FluentValidation.Results.ValidationFailure(field, why)]);
        return at;
    }

    public static IRuleBuilderOptions<T, int> ExpectedVersion<T>(this IRuleBuilder<T, int> rule) =>
        rule.GreaterThan(0).WithMessage("Give the version of the specimen you changed.");
}
