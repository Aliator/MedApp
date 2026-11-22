using MedApp.Application.Common.Identity;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MedApp.API.Common.RateLimiting
{
    /// <summary>The API's sign-in throttling: one person in their own browser has nothing to throttle.</summary>
    public static class LoginThrottling
    {
        public const string Policy = "login";
    }

    public sealed class ThrottleLoginByUsernameAttribute : ActionFilterAttribute;

    public sealed class ThrottleResetRequestsAttribute : ActionFilterAttribute;
}

namespace MedApp.API.Common.Authentication
{
    /// <summary>The API's permission policies' names (<see cref="RequirePermissionAttribute"/>), read back by the router.</summary>
    public static class PermissionPolicies
    {
        private const string Prefix = "Permission:";

        public static string Name(Permission permission) => Prefix + permission;
    }
}
