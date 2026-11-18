using MedApp.Application.Authentication.TwoFactor.Common;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Domain.Authentication;
using MediatR;

namespace MedApp.Application.Authentication.TwoFactor.Commands.BeginEmailCodesSetup;

/// <summary>
/// Refused (409) while two-factor is on (either method: moving to email means turning the app off first), without a
/// confirmed address, or where email can't be sent. Then checks the password (a wrong one is a 400 on <c>Password</c> and
/// counts towards the lockout) and emails a code for turning codes by email on (<see cref="EmailCodePurpose.TurnOn"/>).
/// </summary>
public sealed class BeginEmailCodesSetupHandler(
    ITwoFactorService twoFactor,
    IIdentityUserService users,
    IAccountEmailService emails,
    IEmailSender sender,
    IEmailCodeService emailCodes,
    TimeProvider timeProvider)
    : IRequestHandler<BeginEmailCodesSetupCommand, EmailCodeSentResponse>
{
    public async Task<EmailCodeSentResponse> Handle(BeginEmailCodesSetupCommand request, CancellationToken ct)
    {
        var state = await twoFactor.GetStateAsync(request.UserId, ct)
                    ?? throw new NotFoundException("Account not found.");

        if (state.Enabled)
            throw new ConflictException(
                "Two-factor authentication is already on. Turn it off first to use codes by email instead.");

        if (await EmailCodesAvailability.WhyNotAsync(emails, sender, request.UserId, ct) is { } reason)
            throw new ConflictException(EmailCodesAvailability.Explain(reason));

        // A session alone isn't enough to tie two-factor to a mailbox: the password too.
        await users.VerifyOrThrowAsync(twoFactor, request.UserId, state.Username, request.Password, ct);

        var sent = await emailCodes.SendAsync(request.UserId, EmailCodePurpose.TurnOn, null, ct)
                   ?? throw new ConflictException(EmailCodesAvailability.Explain(EmailCodesAvailability.NoConfirmedAddress));

        return TwoFactorMapper.ToResponse(sent, timeProvider.GetUtcNow().UtcDateTime);
    }
}
