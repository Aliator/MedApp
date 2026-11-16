using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.TwoFactor.Commands.BeginAuthenticatorSetup;

/// <summary>
/// Starts setting up an authenticator app for the signed-in account: a new key, as a QR code and as text. Two-factor
/// stays off until a code from the app confirms it (<c>EnableTwoFactorCommand</c>).
/// </summary>
public sealed record BeginAuthenticatorSetupCommand(
    Guid UserId
) : IRequest<AuthenticatorSetupResponse>;
