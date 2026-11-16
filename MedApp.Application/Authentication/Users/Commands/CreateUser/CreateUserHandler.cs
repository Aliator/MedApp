using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Authentication.Users.Commands.CreateUser;

/// <summary>Creates an account; the activity log records it once it's made.</summary>
public sealed class CreateUserHandler(
    IIdentityUserService userManagementService,
    IActivityLog activityLog)
    : IRequestHandler<CreateUserCommand, CreateUserResult>
{
    public async Task<CreateUserResult> Handle(
        CreateUserCommand request,
        CancellationToken ct)
    {
        var result = await userManagementService.CreateUserAsync(
            request.Username,
            ct);

        if (result.Succeeded)
            await activityLog.RecordAsync(
                activityLog.Entry(ActivityAction.AccountCreated, targetUsername: request.Username),
                ct);

        return result;
    }
}
