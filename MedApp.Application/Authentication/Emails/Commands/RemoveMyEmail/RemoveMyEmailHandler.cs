using MedApp.Application.Common.Email;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MedApp.Domain.Authentication;
using MediatR;

namespace MedApp.Application.Authentication.Emails.Commands.RemoveMyEmail;

/// <summary>
/// Checks the password, then removes the account's addresses. The confirmed one removed is told, so a removal its owner
/// didn't make (to stop reset links reaching them) is noticed. Refused (409) while the account signs in with codes by
/// email: the address is its second factor, so two-factor goes off (or moves to the app) first.
/// </summary>
public sealed class RemoveMyEmailHandler(
    IAccountEmailService emails,
    IIdentityUserService users,
    ITwoFactorService twoFactor,
    ISecurityNotices notices,
    ISecurityEventLog events)
    : IRequestHandler<RemoveMyEmailCommand>
{
    public const string SignsInWithEmailCodes =
        "You sign in with codes sent to this address. Turn off two-factor authentication first, or switch to an authenticator app (Account Settings, Security).";

    public async Task Handle(RemoveMyEmailCommand request, CancellationToken ct)
    {
        var account = await emails.GetAsync(request.UserId, ct)
                      ?? throw new NotFoundException("Account not found.");

        if (await twoFactor.GetMethodAsync(request.UserId, ct) == TwoFactorMethod.Email)
            throw new ConflictException(SignsInWithEmailCodes);

        await users.VerifyOrThrowAsync(twoFactor, request.UserId, account.Username, request.Password, ct);

        var removed = await emails.RemoveAsync(request.UserId, ct)
                      ?? throw new NotFoundException("Account not found.");

        if (removed.Email is null && removed.PendingEmail is null)
            return;

        await events.RecordAsync(
            new SecurityEvent(SecurityEventKind.AddressRemoved, request.UserId, removed.Username),
            ct);

        if (removed.Email is { } address)
            await notices.SendAsync(request.UserId, SecurityNotice.AddressRemoved, ct, toAddress: address);
    }
}
