using FluentValidation;

namespace MedApp.Application.Common.Identity;

/// <summary>
/// A member of staff's full name and job title, as they go on letters: optional, each at most <see cref="MaxLength"/>
/// characters once trimmed; empty means none.
/// </summary>
public static class StaffDetailsRules
{
    public const int MaxLength = 100;

    public const string FullNameField = "fullName";
    public const string JobTitleField = "jobTitle";

    /// <summary>The text trimmed, with runs of spaces made one; null for none.</summary>
    public static string? Tidy(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? null
            : string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static IRuleBuilderOptions<T, string?> StaffDetail<T>(this IRuleBuilder<T, string?> rule, string label) =>
        rule
            .Must(text => (Tidy(text)?.Length ?? 0) <= MaxLength)
            .WithMessage($"{label} must not exceed {MaxLength} characters.");
}

/// <summary>A member of staff's full name and job title (null for one not given).</summary>
public sealed record StaffDetails(string? FullName, string? JobTitle);

/// <summary>Reads and sets staff members' full names and job titles (columns on their accounts).</summary>
public interface IStaffDetailsService
{
    /// <summary>The account's details, or null when there's no such account.</summary>
    Task<StaffDetails?> GetAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Replaces the account's details and saves <paramref name="activity"/> in the same save, so the change can't miss the
    /// activity log. False when there's no such account.
    /// </summary>
    Task<bool> SetAsync(Guid userId, StaffDetails details, Domain.Activity.ActivityEntry activity, CancellationToken ct);
}
