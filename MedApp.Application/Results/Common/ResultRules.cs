using FluentValidation;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Results;

namespace MedApp.Application.Results.Common;

/// <summary>Validation rules shared by the result commands.</summary>
public static class ResultRules
{
    public const string ActionMessage =
        "Choose what was done: NoFurtherAction, PatientInformed, AppointmentArranged, ForMdtDiscussion, ReferredOn or Other.";

    public const string OtherDetailMessage = "Say what was done.";

    public static string? Optional(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    public static IRuleBuilderOptions<T, int> ResultVersion<T>(this IRuleBuilder<T, int> rule) =>
        rule.GreaterThan(0).WithMessage("Give the version of the result you changed.");

    public static IRuleBuilderOptions<T, string?> Title<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("Say what the result is.")
            .MaximumLength(Result.MaxTitleLength)
            .WithMessage($"The title must be at most {Result.MaxTitleLength} characters.");

    public static IRuleBuilderOptions<T, string?> ResultAction<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(a => PathwayEnums.TryParse<ResultAction>(a, out _)).WithMessage(ActionMessage);

    public static IRuleBuilderOptions<T, string?> ActionDetail<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(Result.MaxActionDetailLength)
            .WithMessage($"The detail must be at most {Result.MaxActionDetailLength:N0} characters.");

    public static IRuleBuilderOptions<T, string?> ErrorDetail<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(d => !string.IsNullOrWhiteSpace(d)).WithMessage("Say what was wrong.")
            .MaximumLength(Result.MaxErrorDetailLength)
            .WithMessage($"The detail must be at most {Result.MaxErrorDetailLength} characters.");
}
