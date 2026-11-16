using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;

namespace MedApp.Application.Authentication.TwoFactor.Common;

/// <summary>Two-factor state, a new authenticator key and recovery codes, as the API returns them to their owner.</summary>
public static class TwoFactorMapper
{
    /// <param name="required">Whether the account must use it (<see cref="TwoFactorRequirement"/>).</param>
    public static TwoFactorStatusResponse ToResponse(TwoFactorState state, bool required) =>
        new(state.Enabled, state.Enabled ? state.RecoveryCodesLeft : 0, required);

    /// <summary>A new key, as the address an app reads and the QR code holding it.</summary>
    public static AuthenticatorSetupResponse ToResponse(AuthenticatorKey key, string authenticatorUri, QrCodeImage qrCode) =>
        new(AuthenticatorUri.FormatKey(key.Key), authenticatorUri, qrCode.Path, qrCode.Size);

    public static RecoveryCodesResponse ToResponse(RecoveryCodesResult codes) =>
        new(codes.Codes.ToList());
}
