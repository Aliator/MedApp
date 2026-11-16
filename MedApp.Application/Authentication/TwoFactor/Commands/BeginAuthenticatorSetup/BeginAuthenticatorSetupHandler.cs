using MedApp.Application.Authentication.TwoFactor.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.TwoFactor.Commands.BeginAuthenticatorSetup;

/// <summary>
/// Gives the account a new authenticator key (replacing any unconfirmed one) and draws it as a QR code. Refused (409)
/// while two-factor is on, so a new key can't silently replace the app that works.
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

        if (state.Enabled)
            throw new ConflictException("Two-factor authentication is already on. Turn it off first to set up a different app.");

        var key = await twoFactor.ResetAuthenticatorKeyAsync(request.UserId, ct)
                  ?? throw new NotFoundException("Account not found.");

        var uri = AuthenticatorUri.Build(key.Username, key.Key);
        return TwoFactorMapper.ToResponse(key, uri, qrCodes.Render(uri));
    }
}
