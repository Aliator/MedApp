using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Roles.Commands.SetUserRoles;

public sealed class SetUserRolesHandler(
    IIdentityRoleService roleService,
    IIdentityReadService readService,
    ISessionService sessionService)
    : IRequestHandler<SetUserRolesCommand, IdentityResult>
{
    public async Task<IdentityResult> Handle(
        SetUserRolesCommand request,
        CancellationToken ct)
    {
        // An admin could otherwise take away their own Admin role, and with it the way back.
        if (string.Equals(request.Username, request.ActorUsername, StringComparison.OrdinalIgnoreCase))
            return IdentityResult.Failed(IdentityErrors.OwnRoles);

        var before = await readService.GetUserAsync(request.Username, ct);

        var result = await roleService.SetRolesAsync(request.Username, request.Roles, ct);

        // Their roles changed, so their sessions end, and they sign in again under the new ones. None of them is the
        // admin's own (admins can't change their own roles). Saving the roles they already had changes nothing.
        if (result.Succeeded && before is not null && !SameRoles(before.Roles, request.Roles))
            await sessionService.RevokeUserSessionsAsync(before.UserId, exceptSessionId: null, ct);

        return result;
    }

    private static bool SameRoles(IEnumerable<string> current, IEnumerable<string> wanted) =>
        current.ToHashSet(StringComparer.OrdinalIgnoreCase)
            .SetEquals(wanted.ToHashSet(StringComparer.OrdinalIgnoreCase));
}
