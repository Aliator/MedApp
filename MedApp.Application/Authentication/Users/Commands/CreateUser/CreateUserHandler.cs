using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Authentication.Users.Commands.CreateUser;

/// <summary>
/// Creates the account with a generated password, as before, and the activity log records it once it's made. With an
/// email address, the account is then given it as its pending address, and a link to confirm it is sent there: the
/// welcome is that link, never the password, which the admin passes on themselves.
/// </summary>
public sealed class CreateUserHandler(
    IIdentityUserService userManagementService,
    IActivityLog activityLog,
    IIdentityReadService readService,
    IAccountEmailService emails,
    IEmailSender sender,
    EmailLinks links,
    ISecurityEventLog events)
    : IRequestHandler<CreateUserCommand, CreateUserResult>
{
    public async Task<CreateUserResult> Handle(
        CreateUserCommand request,
        CancellationToken ct)
    {
        var created = await userManagementService.CreateUserAsync(
            request.Username,
            ct);

        if (!created.Succeeded)
            return created;

        await activityLog.RecordAsync(
            activityLog.Entry(ActivityAction.AccountCreated, targetUsername: request.Username),
            ct);

        if (string.IsNullOrWhiteSpace(request.Email))
            return created;

        if (await readService.GetUserAsync(request.Username, ct) is not { } user)
            return created;

        var change = await emails.RequestChangeAsync(user.UserId, request.Email.Trim(), ct);
        if (change?.Token is { } token && links.ConfirmEmail(user.UserId, token) is { } link)
        {
            await sender.SendAsync(EmailTemplates.ConfirmEmail(change.Email, change.Username, link), ct);
            await events.RecordAsync(
                new SecurityEvent(SecurityEventKind.EmailChangeRequested, user.UserId, change.Username),
                ct);
        }

        return created;
    }
}
