using MedApp.Application.Common.Identity;
using Microsoft.AspNetCore.Authorization;

namespace MedApp.API.Common.Authentication;

/// <summary>
/// Lets a request through only when the signed-in user's roles grant the permission (the session's permission
/// claims): <c>[RequirePermission(Permission.ViewPatients)]</c>. Several on one action (or its controller) must all
/// pass. A refusal is a 403 with <c>code: "permission_missing"</c> and the permission's description
/// (<see cref="ApiAuthorizationResultHandler"/>). Every controller action names one, or is <c>[AllowAnonymous]</c>, or
/// is one of the few plain <c>[Authorize]</c> ones (a convention test checks).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute(Permission permission) : AuthorizeAttribute(PermissionPolicies.Name(permission))
{
    public Permission Permission { get; } = permission;
}
