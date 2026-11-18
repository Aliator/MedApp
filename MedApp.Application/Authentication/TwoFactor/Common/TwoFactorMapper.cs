using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Domain.Authentication;

namespace MedApp.Application.Authentication.TwoFactor.Common;

/// <summary>Two-factor state, a new authenticator key and recovery codes, as the API returns them to their owner.</summary>
public static class TwoFactorMapper
{
    /// <param name="required">Whether the account must use it (<see cref="TwoFactorRequirement"/>).</param>
    /// <param name="emailCodesUnavailable">
    /// Why it couldn't turn codes by email on (<see cref="EmailCodesAvailability"/>), or null if it could.
    /// </param>
    public static TwoFactorStatusResponse ToResponse(TwoFactorState state, bool required, string? emailCodesUnavailable) =>
        new(
            state.Enabled,
            state.Enabled ? state.RecoveryCodesLeft : 0,
            required,
            (state.UsesEmailCodes ? TwoFactorMethod.Email : state.Enabled ? TwoFactorMethod.Authenticator : TwoFactorMethod.None).ToString(),
            emailCodesUnavailable is null,
            emailCodesUnavailable);

    /// <summary>A code emailed, with the wait before it can be sent again by the server's clock at <paramref name="utcNow"/>.</summary>
    public static EmailCodeSentResponse ToResponse(EmailCodeSent sent, DateTime utcNow) =>
        new(
            sent.MaskedAddress,
            sent.ExpiresAtUtc,
            sent.ResendsLeft == 0 ? 0 : Math.Max(0, (int)Math.Ceiling((sent.ResendAvailableAtUtc - utcNow).TotalSeconds)),
            sent.ResendsLeft);

    /// <summary>A new key, as the address an app reads and the QR code holding it.</summary>
    public static AuthenticatorSetupResponse ToResponse(AuthenticatorKey key, string authenticatorUri, QrCodeImage qrCode) =>
        new(AuthenticatorUri.FormatKey(key.Key), authenticatorUri, qrCode.Path, qrCode.Size);

    public static RecoveryCodesResponse ToResponse(RecoveryCodesResult codes) =>
        new(codes.Codes.ToList());
}
