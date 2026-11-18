using FluentValidation;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.Common;

/// <summary>
/// The reasons a note is deleted, or asked to be (<see cref="NoteDeletionReason"/>): sent and returned by name, shown in
/// words, with an optional detail (required for <see cref="NoteDeletionReason.Other"/>).
/// </summary>
public static class NoteDeletionReasons
{
    public static string Names => string.Join(", ", Enum.GetNames<NoteDeletionReason>());

    /// <summary>The reason a name stands for, ignoring case; false for anything else.</summary>
    public static bool TryParse(string? name, out NoteDeletionReason reason) => ProcedureEnums.TryParse(name, out reason);

    /// <summary>A reason in words: "Wrong patient".</summary>
    public static string Label(NoteDeletionReason reason) => reason switch
    {
        NoteDeletionReason.WrongPatient => "Wrong patient",
        NoteDeletionReason.WrittenInError => "Written in error",
        NoteDeletionReason.Duplicate => "Duplicate",
        NoteDeletionReason.Other => "Other",
        _ => reason.ToString()
    };

    /// <summary>A reason and its detail in words: "Wrong patient: belongs to the other procedure."</summary>
    public static string Describe(NoteDeletionReason reason, string? detail) =>
        string.IsNullOrWhiteSpace(detail) ? Label(reason) : $"{Label(reason)}: {detail}";

    /// <summary>A detail, trimmed, or null when there's none.</summary>
    public static string? CleanDetail(string? detail) => string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();

    /// <summary>The reason's name: one of <see cref="Names"/>.</summary>
    public static IRuleBuilderOptions<T, string?> DeletionReason<T>(this IRuleBuilder<T, string?> rule) =>
        rule
            .Must(name => TryParse(name, out _))
            .WithMessage($"Choose why the note should be deleted: one of {Names}.");

    /// <summary>The detail: at most 500 characters.</summary>
    public static IRuleBuilderOptions<T, string?> DeletionDetail<T>(this IRuleBuilder<T, string?> rule) =>
        rule
            .MaximumLength(PatientProcedureNote.MaxDeletionDetailLength)
            .WithMessage($"The detail must not exceed {PatientProcedureNote.MaxDeletionDetailLength} characters.");

    public const string OtherNeedsDetailMessage = "Say why, when the reason is Other.";

    /// <summary>Whether a reason's name is Other, which needs a detail.</summary>
    public static bool IsOther(string? name) => TryParse(name, out var reason) && reason == NoteDeletionReason.Other;
}
