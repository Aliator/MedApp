using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Authentication.Security.Repositories;
using MedApp.Application.Authentication.TwoFactor.Common;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.TwoFactor.Commands.DisableTwoFactor;

/// <summary>
/// Turns two-factor off, after checking the password (a wrong one counts towards the lockout, as everywhere) and a
/// current code (which does too). Refused (409) for an account that must use it. Turning it off and ending the other
/// sessions happen together. The owner is told by email.
/// </summary>
public sealed class DisableTwoFactorHandler(
    ITwoFactorService twoFactor,
    IIdentityUserService users,
    ISecuritySettingsRepository settings,
    ISessionService sessionService,
    IIdentityTransactions transactions,
    ISecurityNotices notices,
    ISecurityEventLog events)
    : IRequestHandler<DisableTwoFactorCommand>
{
    public async Task Handle(DisableTwoFactorCommand request, CancellationToken ct)
    {
        var state = await twoFactor.GetStateAsync(request.UserId, ct)
                    ?? throw new NotFoundException("Account not found.");

        if (!state.Enabled)
            throw new ConflictException("Two-factor authentication is already off.");

        if (await settings.IsRequiredAsync(state, ct))
            throw new ConflictException("Admin accounts must use two-factor authentication here, so it can't be turned off.");

        var password = await users.VerifyPasswordAsync(state.Username, request.Password, ct);
        if (!password.Succeeded)
        {
            if (password.Errors.Any(e => e.Code == IdentityErrors.LockedOutCode)
                && await twoFactor.LockoutMinutesLeftAsync(request.UserId, ct) is { } lockedFor)
                throw new AccountLockedOutException(lockedFor);

            throw new ValidationException(
                [new ValidationFailure(nameof(request.Password), IdentityErrors.PasswordIncorrect.Description)]);
        }

        await twoFactor.VerifyOrThrowAsync(request.UserId, request.Code, nameof(request.Code), allowRecoveryCode: true, ct);

        var result = await transactions.RunAsync(async () =>
        {
            var disabled = await twoFactor.DisableAsync(request.UserId, ct);
            if (!disabled.Succeeded)
                return disabled;

            await sessionService.RevokeUserSessionsAsync(request.UserId, request.CurrentSessionId, ct);
            return IdentityResult.Success;
        }, ct);

        if (!result.Succeeded)
            throw new ConflictException(string.Join(" ", result.Errors.Select(e => e.Description)));

        await events.RecordAsync(new SecurityEvent(SecurityEventKind.TwoFactorDisabled, request.UserId, state.Username), ct);
        await notices.SendAsync(request.UserId, SecurityNotice.TwoFactorDisabled, ct);
    }
}
