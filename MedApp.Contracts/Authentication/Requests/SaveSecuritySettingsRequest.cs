namespace MedApp.Contracts.Authentication.Requests;

/// <summary>The deployment's sign-in rules, which admins set.</summary>
public sealed record SaveSecuritySettingsRequest(
    bool RequireTwoFactorForAdmins);
