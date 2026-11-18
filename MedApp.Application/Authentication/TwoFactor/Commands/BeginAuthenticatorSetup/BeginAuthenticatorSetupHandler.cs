using MedApp.Application.Authentication.TwoFactor.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.TwoFactor.Commands.BeginAuthenticatorSetup;

/// <summary>
/// Gives the account a new authenticator key (replacing any unconfirmed one) and draws it as a QR code. Refused (409)
/// while two-factor is on with the app, so a new key can't silently replace the app that works. Allowed while it's on
/// with codes by email: turning the app on then replaces them, so someone can move to the stronger method (and an admin
/// held by the requirement, which needs the app, can) without two-factor ever being off.
/// </summary>
public sealed class BeginAuthenticatorSetupHandler(
    ITwoFactorService twoFactor,
    IQrCodeRenderer qrCodes)
    : IRequestHandler<BeginAuthenticatorSetupCommand, AuthenticatorSetupResponse>
{
    public async Task<AuthenticatorSetupResponse> Handle(BeginAuthenticatorSetupCommand request, CancellationToken ct)
    {
        var state = await twoFactor.GetStateAsync(request.UserId, ct)
                    ?? throw new NotFoundException("Account not found.");

        if (state.UsesApp)
            throw new ConflictException("Two-factor authentication is already on. Turn it off first to set up a different app.");

        var key = await twoFactor.ResetAuthenticatorKeyAsync(request.UserId, ct)
                  ?? throw new NotFoundException("Account not found.");

        var uri = AuthenticatorUri.Build(key.Username, key.Key);
        return TwoFactorMapper.ToResponse(key, uri, qrCodes.Render(uri));
    }
}
