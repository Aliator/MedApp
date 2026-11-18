using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.TwoFactor.Commands.SendTwoFactorEmailCode;

/// <summary>
/// Emails the signed-in account a code to confirm a change to its two-factor authentication (turning it off, new
/// recovery codes), when it uses codes by email. Asking again sends a new code, within the limits on sending again.
/// </summary>
public sealed record SendTwoFactorEmailCodeCommand(
    Guid UserId
) : IRequest<EmailCodeSentResponse>;
