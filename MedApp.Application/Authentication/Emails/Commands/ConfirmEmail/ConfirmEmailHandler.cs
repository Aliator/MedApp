using MedApp.Application.Common.Email;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Emails.Commands.ConfirmEmail;

/// <summary>
/// Confirms the pending address: it becomes the account's address, used for reset links and notices. The address it
/// replaced (if any) is told, so a change its owner didn't make is noticed. A wrong, used or expired link is
/// <see cref="IdentityErrors.InvalidToken"/>, whatever the reason; a link for another account than the one signed in is
/// <see cref="IdentityErrors.WrongAccount"/>.
/// </summary>
public sealed class ConfirmEmailHandler(
    IAccountEmailService emails,
    ISecurityNotices notices,
    ISecurityEventLog events)
    : IRequestHandler<ConfirmEmailCommand, IdentityResult>
{
    public async Task<IdentityResult> Handle(ConfirmEmailCommand request, CancellationToken ct)
    {
        // Whoever reads the mailbox isn't necessarily the person the account is for (a mistyped or shared address):
        // only that account, signed in, can confirm it.
        if (request.SignedInUserId != request.UserId)
            return IdentityResult.Failed(IdentityErrors.WrongAccount);

        var confirmation = await emails.ConfirmAsync(request.UserId, request.Token, ct);
        if (!confirmation.Result.Succeeded)
            return confirmation.Result;

        await events.RecordAsync(
            new SecurityEvent(SecurityEventKind.AddressConfirmed, request.UserId, confirmation.Username),
            ct);

        if (confirmation.PreviousEmail is { } previous)
            await notices.SendAsync(request.UserId, SecurityNotice.AddressChanged, ct, toAddress: previous);

        return confirmation.Result;
    }
}
