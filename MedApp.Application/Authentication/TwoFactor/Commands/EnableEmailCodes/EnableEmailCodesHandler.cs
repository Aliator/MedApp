using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Authentication.TwoFactor.Common;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Domain.Authentication;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.TwoFactor.Commands.EnableEmailCodes;

/// <summary>
/// Checks the emailed code (it counts against the account's limit on codes, and against the code's own attempts; the
/// password was checked when it was sent), then turns two-factor on with codes by email, with new recovery codes, and
/// ends the account's other sessions, together (one transaction). A wrong code is a validation error on <c>Code</c>; as
/// with setting up the app, it doesn't count towards the lockout. The address is checked again first (it may have been
/// removed since the code was sent), and the code itself works only for the address it was sent to. Recorded as
/// <see cref="SecurityEventKind.EmailCodesEnabled"/>, and the owner (and, for an admin, the other admins) told by email.
/// </summary>
public sealed class EnableEmailCodesHandler(
    ITwoFactorService twoFactor,
    IEmailCodeService emailCodes,
    IAccountEmailService emails,
    IEmailSender sender,
    ISessionService sessionService,
    IIdentityTransactions transactions,
    ISecurityNotices notices,
    ISecurityEventLog events,
    IAccountAttemptLimiter attempts)
    : IRequestHandler<EnableEmailCodesCommand, RecoveryCodesResponse>
{
    public async Task<RecoveryCodesResponse> Handle(EnableEmailCodesCommand request, CancellationToken ct)
    {
        var state = await twoFactor.GetStateAsync(request.UserId, ct)
                    ?? throw new NotFoundException("Account not found.");

        if (state.Enabled)
            throw new ConflictException("Two-factor authentication is already on.");

        // The address may have gone since the code was sent (removed, or email switched off): check again.
        if (await EmailCodesAvailability.WhyNotAsync(emails, sender, request.UserId, ct) is { } reason)
            throw new ConflictException(EmailCodesAvailability.Explain(reason));

        attempts.Attempt(AccountAttempt.TwoFactorCode, request.UserId);
        var check = await emailCodes.CheckAsync(request.UserId, EmailCodePurpose.TurnOn, null, request.Code, ct);
        if (check != EmailCodeCheck.Right)
            throw new ValidationException([new ValidationFailure(
                nameof(request.Code), check == EmailCodeCheck.NotLive ? TwoFactorCodes.EmailCodeNotLive : TwoFactorCodes.WrongEmailCode)]);

        RecoveryCodesResult? codes = null;
        var result = await transactions.RunAsync(async () =>
        {
            codes = await twoFactor.EnableAsync(request.UserId, TwoFactorMethod.Email, ct);
            if (!codes.Result.Succeeded)
                return codes.Result;

            // Signing in now needs a code: sessions started with the password alone, elsewhere, end.
            await sessionService.RevokeUserSessionsAsync(request.UserId, request.CurrentSessionId, ct);
            return IdentityResult.Success;
        }, ct);

        if (!result.Succeeded)
            throw new ConflictException(string.Join(" ", result.Errors.Select(e => e.Description)));

        await events.RecordAsync(new SecurityEvent(SecurityEventKind.EmailCodesEnabled, request.UserId, state.Username), ct);
        await notices.SendAsync(request.UserId, SecurityNotice.EmailCodesEnabled, ct);
        if (state.IsAdmin)
            await notices.SendToOtherAdminsAsync(SecurityNotice.AdminTwoFactorEnabled, request.UserId, state.Username, ct);

        return TwoFactorMapper.ToResponse(codes!);
    }
}
