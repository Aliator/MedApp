namespace MedApp.Contracts.Authentication.Responses;

/// <summary>The deployment's sign-in rules, which admins set.</summary>
/// <param name="RequireTwoFactorForAdmins">Whether admin accounts must use two-factor authentication.</param>
/// <param name="AdminsWithoutTwoFactor">How many admin accounts don't have it on yet.</param>
/// <param name="UpdatedAtUtc">When an admin last changed them; null if never.</param>
/// <param name="UpdatedBy">Who did; null if never, or if their account is gone.</param>
/// <param name="AdminsWithoutTwoFactorUsernames">Which admin accounts don't have it on yet, by username.</param>
public sealed record SecuritySettingsResponse(
    bool RequireTwoFactorForAdmins,
    int AdminsWithoutTwoFactor,
    DateTime? UpdatedAtUtc,
    string? UpdatedBy,
    IReadOnlyList<string> AdminsWithoutTwoFactorUsernames);
