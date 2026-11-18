namespace MedApp.Contracts.Authentication.Responses;

/// <summary>The signed-in account's two-factor authentication.</summary>
/// <param name="Enabled">Whether signing in needs a code as well as the password.</param>
/// <param name="RecoveryCodesLeft">Unused recovery codes (0 when it's off).</param>
/// <param name="Required">
/// Whether this account must use it (an admin, while admins are required to), so it can't be turned off. The requirement
/// needs the authenticator app: an admin with codes by email is held to switch.
/// </param>
/// <param name="Method"><c>None</c>, <c>Authenticator</c> or <c>Email</c>.</param>
/// <param name="CanUseEmailCodes">Whether codes by email could be turned on: a confirmed address, and email this server can send.</param>
/// <param name="EmailCodesUnavailableReason">
/// Why not, when they can't: <c>NoConfirmedAddress</c> or <c>EmailUnavailable</c>; otherwise null.
/// </param>
public sealed record TwoFactorStatusResponse(
    bool Enabled,
    int RecoveryCodesLeft,
    bool Required,
    string Method = "None",
    bool CanUseEmailCodes = false,
    string? EmailCodesUnavailableReason = null);
