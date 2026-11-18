using MedApp.Application.Authentication.TwoFactor.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Domain.Authentication;
using MediatR;

namespace MedApp.Application.Authentication.TwoFactor.Commands.SendTwoFactorEmailCode;

/// <summary>
/// Emails a code for confirming a change (<see cref="EmailCodePurpose.Confirm"/>: turning two-factor off, new recovery
/// codes, switching to the app, a new address) to the account's confirmed address. Refused (409) unless the account uses
/// codes by email. Asks for the password first, as turning codes on does (a wrong one is a 400 on <c>Password</c> and
/// counts towards the lockout), so a session alone can't fill the owner's mailbox. A 409 too when the email can't go now.
/// </summary>
public sealed class SendTwoFactorEmailCodeHandler(
    ITwoFactorService twoFactor,
    IIdentityUserService users,
    IEmailCodeService emailCodes,
    TimeProvider timeProvider)
    : IRequestHandler<SendTwoFactorEmailCodeCommand, EmailCodeSentResponse>
{
    public const string CouldNotSend =
        "MedApp couldn't email you a code just now (there's no confirmed address, or email isn't available). Use a recovery code instead, or try again later.";

    public async Task<EmailCodeSentResponse> Handle(SendTwoFactorEmailCodeCommand request, CancellationToken ct)
    {
        var state = await twoFactor.GetStateAsync(request.UserId, ct)
                    ?? throw new NotFoundException("Account not found.");

        if (!state.UsesEmailCodes)
            throw new ConflictException("Your account doesn't use codes by email.");

        await users.VerifyOrThrowAsync(twoFactor, request.UserId, state.Username, request.Password, ct);

        var sent = await emailCodes.SendAsync(request.UserId, EmailCodePurpose.Confirm, null, ct)
                   ?? throw new ConflictException(CouldNotSend);

        return TwoFactorMapper.ToResponse(sent, timeProvider.GetUtcNow().UtcDateTime);
    }
}
