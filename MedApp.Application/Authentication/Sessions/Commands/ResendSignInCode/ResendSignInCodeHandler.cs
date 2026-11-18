using MedApp.Application.Authentication.TwoFactor.Common;
using MedApp.Application.Authentication.TwoFactor.Repositories;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Domain.Authentication;
using MediatR;

namespace MedApp.Application.Authentication.Sessions.Commands.ResendSignInCode;

/// <summary>
/// Sends the sign-in's code again, as a new code, to the account's confirmed address: only for a usable challenge (a 401
/// <c>sign_in_expired</c> otherwise, as for a code), only for an account using codes by email (a 409), and at most once
/// a minute and <see cref="EmailCode.MaxResends"/> times per challenge (a 429, with how long to wait), within the
/// account's limit on emailed codes. Sending again doesn't give the challenge more attempts or more time.
/// </summary>
public sealed class ResendSignInCodeHandler(
    ISignInChallengeRepository challenges,
    ITwoFactorService twoFactor,
    IEmailCodeService emailCodes,
    TimeProvider timeProvider)
    : IRequestHandler<ResendSignInCodeCommand, EmailCodeSentResponse>
{
    public async Task<EmailCodeSentResponse> Handle(ResendSignInCodeCommand request, CancellationToken ct)
    {
        var challenge = await challenges.FindByTokenHashAsync(SignInChallengeTokens.Hash(request.Challenge), ct);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (challenge is null || !challenge.IsUsable(now))
            throw new SignInExpiredException();

        if (await twoFactor.GetMethodAsync(challenge.UserId, ct) != TwoFactorMethod.Email)
            throw new ConflictException("This account doesn't use codes by email.");

        var sent = await emailCodes.SendAsync(challenge.UserId, EmailCodePurpose.SignIn, challenge.Id, ct)
                   ?? throw new ConflictException(
                       "There's no confirmed email address to send a code to. Use a recovery code instead.");

        return TwoFactorMapper.ToResponse(sent, now);
    }
}
