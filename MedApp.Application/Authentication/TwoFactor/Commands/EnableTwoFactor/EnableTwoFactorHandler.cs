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
/// <c>switchedFrom: "email"</c>.
/// </summary>
public sealed class EnableTwoFactorHandler(
    ITwoFactorService twoFactor,
    IIdentityUserService users,
    ISessionService sessionService,
    IIdentityTransactions transactions,
    ISecurityNotices notices,
    ISecurityEventLog events)
    : IRequestHandler<EnableTwoFactorCommand, RecoveryCodesResponse>
{
    public async Task<RecoveryCodesResponse> Handle(EnableTwoFactorCommand request, CancellationToken ct)
    {
        var state = await twoFactor.GetStateAsync(request.UserId, ct)
                    ?? throw new NotFoundException("Account not found.");

        if (state.UsesApp)
            throw new ConflictException("Two-factor authentication is already on.");
        var switchingFromEmail = state.UsesEmailCodes;

        // A session alone isn't enough to tie the account to someone's app: the password too.
        await users.VerifyOrThrowAsync(twoFactor, request.UserId, state.Username, request.Password, ct);

        if (!await twoFactor.VerifyAuthenticatorCodeAsync(request.UserId, request.Code, ct))
            throw new ValidationException([new ValidationFailure(nameof(request.Code), TwoFactorCodes.WrongCode)]);

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
        await notices.SendAsync(request.UserId, SecurityNotice.TwoFactorEnabled, ct);
        // Other admins hear when an admin turns two-factor on, not when one moves to the stronger method.
        if (state.IsAdmin && !switchingFromEmail)
            await notices.SendToOtherAdminsAsync(SecurityNotice.AdminTwoFactorEnabled, request.UserId, state.Username, ct);

        return TwoFactorMapper.ToResponse(codes!);
    }
}
