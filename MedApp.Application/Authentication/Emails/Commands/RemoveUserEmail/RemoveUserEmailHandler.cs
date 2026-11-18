using MedApp.Application.Common.Email;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MedApp.Domain.Authentication;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Emails.Commands.RemoveUserEmail;

/// <summary>
/// Removes the account's addresses. It's recorded with the admin who did it (the account as its target), and the
/// confirmed address removed is told. Fails with <see cref="IdentityErrors.UserNotFound"/> for an unknown account, and is
/// refused (409) while the account signs in with codes by email: removing its address would leave only its recovery
/// codes, so its two-factor is reset first.
/// </summary>
public sealed class RemoveUserEmailHandler(
    IIdentityReadService readService,
    IAccountEmailService emails,
    ITwoFactorService twoFactor,
    ISecurityNotices notices,
    ISecurityEventLog events)
    : IRequestHandler<RemoveUserEmailCommand, IdentityResult>
{
    public const string SignsInWithEmailCodes =
        "This account signs in with codes sent to its email address. Reset its two-factor authentication first, then remove the address.";

    public async Task<IdentityResult> Handle(RemoveUserEmailCommand request, CancellationToken ct)
    {
        var user = await readService.GetUserAsync(request.Username, ct);
        if (user is null)
            return IdentityResult.Failed(IdentityErrors.UserNotFound);

        if (await twoFactor.GetMethodAsync(user.UserId, ct) == TwoFactorMethod.Email)
            throw new ConflictException(SignsInWithEmailCodes);

        var removed = await emails.RemoveAsync(user.UserId, ct);
        if (removed is null)
            return IdentityResult.Failed(IdentityErrors.UserNotFound);

        if (removed.Email is null && removed.PendingEmail is null)
            return IdentityResult.Success;

        await events.RecordAsync(
            new SecurityEvent(SecurityEventKind.AddressRemoved, user.UserId, user.Username, request.ActorUserId, request.ActorUsername),
            ct);

        if (removed.Email is { } address)
            await notices.SendAsync(user.UserId, SecurityNotice.AddressRemoved, ct, toAddress: address);

        return IdentityResult.Success;
    }
}
