using MedApp.Application.Common.Email;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Emails.Commands.RemoveUserEmail;

/// <summary>
/// Removes the account's addresses. It's recorded with the admin who did it (the account as its target), and the
/// confirmed address removed is told. Fails with <see cref="IdentityErrors.UserNotFound"/> for an unknown account.
/// </summary>
public sealed class RemoveUserEmailHandler(
    IIdentityReadService readService,
    IAccountEmailService emails,
    ISecurityNotices notices,
    ISecurityEventLog events)
    : IRequestHandler<RemoveUserEmailCommand, IdentityResult>
{
    public async Task<IdentityResult> Handle(RemoveUserEmailCommand request, CancellationToken ct)
    {
        var user = await readService.GetUserAsync(request.Username, ct);
        if (user is null)
            return IdentityResult.Failed(IdentityErrors.UserNotFound);

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
