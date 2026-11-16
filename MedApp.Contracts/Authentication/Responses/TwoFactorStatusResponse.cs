namespace MedApp.Contracts.Authentication.Responses;

/// <summary>The signed-in account's two-factor authentication.</summary>
/// <param name="Enabled">Whether signing in needs a code from an authenticator app.</param>
/// <param name="RecoveryCodesLeft">Unused recovery codes (0 when it's off).</param>
/// <param name="Required">Whether this account must use it (an admin, while admins are required to), so it can't be turned off.</param>
public sealed record TwoFactorStatusResponse(
    bool Enabled,
    int RecoveryCodesLeft,
    bool Required);
