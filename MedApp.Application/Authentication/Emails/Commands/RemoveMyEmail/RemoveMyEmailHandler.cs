using MedApp.Application.Common.Email;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MediatR;

namespace MedApp.Application.Authentication.Emails.Commands.RemoveMyEmail;

/// <summary>
/// Removes the account's addresses. The confirmed one removed is told, so a removal its owner didn't make (to stop
/// reset links reaching them) is noticed.
/// </summary>
public sealed class RemoveMyEmailHandler(
    IAccountEmailService emails,
    ISecurityNotices notices,
    ISecurityEventLog events)
    : IRequestHandler<RemoveMyEmailCommand>
{
    public async Task Handle(RemoveMyEmailCommand request, CancellationToken ct)
    {
        var removed = await emails.RemoveAsync(request.UserId, ct)
                      ?? throw new NotFoundException("Account not found.");

        if (removed.Email is null && removed.PendingEmail is null)
            return;

        await events.RecordAsync(
            new SecurityEvent(SecurityEventKind.EmailRemoved, request.UserId, removed.Username),
            ct);

        if (removed.Email is { } address)
            await notices.SendAsync(request.UserId, SecurityNotice.EmailRemoved, ct, toAddress: address);
    }
}
