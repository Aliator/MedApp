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

namespace MedApp.Application.Authentication.TwoFactor.Commands.EnableTwoFactor;

/// <summary>
/// Checks the password, then the code against the key just set up, then turns two-factor on with new recovery codes and ends the account's
/// other sessions, together (one transaction). A wrong code is a validation error on <c>Code</c>; it doesn't count
/// towards the lockout, since the person setting up holds the key anyway. The owner is told by email. On an account
/// using codes by email, the app replaces them (two-factor stays on throughout), recorded as turned on with
/// <c>switchedFrom: "email"</c>; that switch needs the current factor too (<see cref="EnableTwoFactorCommand.CurrentCode"/>:
/// an emailed code or a recovery code, counted as any code is), and the owner and other admins are told.
/// </summary>
public sealed class EnableTwoFactorHandler(
    ITwoFactorService twoFactor,
    IIdentityUserService users,
    ISessionService sessionService,
    IIdentityTransactions transactions,
    ISecurityNotices notices,
    ISecurityEventLog events,
    IEmailCodeService emailCodes,
    IAccountAttemptLimiter attempts)
    : IRequestHandler<EnableTwoFactorCommand, RecoveryCodesResponse>
{
    public const string CurrentCodeNeeded =
        "Enter the code we emailed you, or one of your recovery codes, to confirm it's you before switching.";

    public async Task<RecoveryCodesResponse> Handle(EnableTwoFactorCommand request, CancellationToken ct)
    {
        var state = await twoFactor.GetStateAsync(request.UserId, ct)
                    ?? throw new NotFoundException("Account not found.");

        if (state.UsesApp)
            throw new ConflictException("Two-factor authentication is already on.");
        var switchingFromEmail = state.UsesEmailCodes;

        // A session alone isn't enough to tie the account to someone's app: the password too.
        await users.VerifyOrThrowAsync(twoFactor, request.UserId, state.Username, request.Password, ct);

        // Replacing codes by email needs them too (or a recovery code), as turning them off does: otherwise a session and
        // the password could swap the owner's second factor for the taker's phone.
        if (switchingFromEmail && string.IsNullOrWhiteSpace(request.CurrentCode))
            throw new ValidationException([new ValidationFailure(nameof(request.CurrentCode), CurrentCodeNeeded)]);

        if (!await twoFactor.VerifyAuthenticatorCodeAsync(request.UserId, TwoFactorCodes.Normalise(request.Code), ct))
            throw new ValidationException([new ValidationFailure(nameof(request.Code), TwoFactorCodes.WrongCode)]);

        if (switchingFromEmail)
            await twoFactor.VerifyOrThrowAsync(
                emailCodes, attempts, notices, request.UserId, TwoFactorMethod.Email, request.CurrentCode!,
                nameof(request.CurrentCode), allowRecoveryCode: true, ct);

        RecoveryCodesResult? codes = null;
        var result = await transactions.RunAsync(async () =>
        {
            codes = await twoFactor.EnableAsync(request.UserId, TwoFactorMethod.Authenticator, ct);
            if (!codes.Result.Succeeded)
                return codes.Result;

            // Signing in now needs the app: sessions started with the password alone, elsewhere, end.
            await sessionService.RevokeUserSessionsAsync(request.UserId, request.CurrentSessionId, ct);
            return IdentityResult.Success;
        }, ct);

        if (!result.Succeeded)
            throw new ConflictException(string.Join(" ", result.Errors.Select(e => e.Description)));

        await events.RecordAsync(
            new SecurityEvent(SecurityEventKind.TwoFactorEnabled, request.UserId, state.Username,
                Details: switchingFromEmail ? new Dictionary<string, object?> { ["switchedFrom"] = "email" } : null),
            ct);
        await notices.SendAsync(
            request.UserId, switchingFromEmail ? SecurityNotice.TwoFactorSwitchedToApp : SecurityNotice.TwoFactorEnabled, ct);
        // Other admins hear when an admin's second factor changes, a switch included, as they do when it goes off.
        if (state.IsAdmin)
            await notices.SendToOtherAdminsAsync(
                switchingFromEmail ? SecurityNotice.AdminTwoFactorSwitchedToApp : SecurityNotice.AdminTwoFactorEnabled,
                request.UserId, state.Username, ct);

        return TwoFactorMapper.ToResponse(codes!);
    }
}
