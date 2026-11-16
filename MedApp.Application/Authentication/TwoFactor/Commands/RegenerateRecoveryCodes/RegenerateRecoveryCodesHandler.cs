using MedApp.Application.Authentication.TwoFactor.Common;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.TwoFactor.Commands.RegenerateRecoveryCodes;

/// <summary>
/// Checks the password and a current authenticator code (wrong ones count towards the lockout, and codes towards the
/// account's limit), then makes a new set of recovery codes. The owner is told by email.
/// </summary>
public sealed class RegenerateRecoveryCodesHandler(
    ITwoFactorService twoFactor,
    IIdentityUserService users,
    ISecurityNotices notices,
    ISecurityEventLog events,
    IAccountAttemptLimiter attempts)
    : IRequestHandler<RegenerateRecoveryCodesCommand, RecoveryCodesResponse>
{
    public async Task<RecoveryCodesResponse> Handle(RegenerateRecoveryCodesCommand request, CancellationToken ct)
    {
        var state = await twoFactor.GetStateAsync(request.UserId, ct)
                    ?? throw new NotFoundException("Account not found.");

        if (!state.Enabled)
            throw new ConflictException("Two-factor authentication is off, so there are no recovery codes.");

        await users.VerifyOrThrowAsync(twoFactor, request.UserId, state.Username, request.Password, ct);
        await twoFactor.VerifyOrThrowAsync(attempts, notices, request.UserId, request.Code, nameof(request.Code), allowRecoveryCode: false, ct);

        var codes = await twoFactor.RegenerateRecoveryCodesAsync(request.UserId, ct);
        if (!codes.Result.Succeeded)
            throw new ConflictException(string.Join(" ", codes.Result.Errors.Select(e => e.Description)));

        await events.RecordAsync(new SecurityEvent(SecurityEventKind.RecoveryCodesRegenerated, request.UserId, state.Username), ct);
        await notices.SendAsync(request.UserId, SecurityNotice.RecoveryCodesRegenerated, ct);

        return TwoFactorMapper.ToResponse(codes);
    }
}
