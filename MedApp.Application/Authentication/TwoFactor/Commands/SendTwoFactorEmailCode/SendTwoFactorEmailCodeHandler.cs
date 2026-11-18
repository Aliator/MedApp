using MedApp.Application.Authentication.TwoFactor.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Domain.Authentication;
using MediatR;

namespace MedApp.Application.Authentication.TwoFactor.Commands.SendTwoFactorEmailCode;

/// <summary>
/// Emails a code for confirming a change (<see cref="EmailCodePurpose.Confirm"/>) to the account's confirmed address.
/// Refused (409) unless the account uses codes by email. It needs no password: it only emails the account's own
/// address, within the limits, and the change it's for asks for the password too.
/// </summary>
public sealed class SendTwoFactorEmailCodeHandler(
    ITwoFactorService twoFactor,
    IEmailCodeService emailCodes,
    TimeProvider timeProvider)
    : IRequestHandler<SendTwoFactorEmailCodeCommand, EmailCodeSentResponse>
{
    public async Task<EmailCodeSentResponse> Handle(SendTwoFactorEmailCodeCommand request, CancellationToken ct)
    {
        var state = await twoFactor.GetStateAsync(request.UserId, ct)
                    ?? throw new NotFoundException("Account not found.");

        if (!state.UsesEmailCodes)
            throw new ConflictException("Your account doesn't use codes by email.");

        var sent = await emailCodes.SendAsync(request.UserId, EmailCodePurpose.Confirm, null, ct)
                   ?? throw new ConflictException(
                       "Your account has no confirmed email address to send a code to. Use a recovery code instead.");

        return TwoFactorMapper.ToResponse(sent, timeProvider.GetUtcNow().UtcDateTime);
    }
}
