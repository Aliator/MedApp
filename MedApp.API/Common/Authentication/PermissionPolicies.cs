using MedApp.Application.Common.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace MedApp.API.Common.Authentication;

/// <summary>The requirement of a permission policy: the user's roles grant <see cref="Permission"/>.</summary>
public sealed class PermissionRequirement(Permission permission) : IAuthorizationRequirement
{
    public Permission Permission { get; } = permission;

    public override string ToString() => $"Permission {Permission}";
}

/// <summary>Passes a <see cref="PermissionRequirement"/> when the session's permission claims include it.</summary>
public sealed class PermissionRequirementHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasPermission(requirement.Permission))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Makes one policy per permission, named <c>Permission:</c> and the permission's name
/// (<see cref="RequirePermissionAttribute"/>): a signed-in user whose roles grant it. Any other name goes to the
/// policies registered as usual.
/// </summary>
public sealed class PermissionPolicies(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    private const string Prefix = "Permission:";

    /// <summary>The policy name for a permission.</summary>
    public static string Name(Permission permission) => Prefix + permission;

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(Prefix, StringComparison.Ordinal)
            && Permissions.TryParse(policyName[Prefix.Length..], out var permission))
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();

        return await base.GetPolicyAsync(policyName);
    }
}
