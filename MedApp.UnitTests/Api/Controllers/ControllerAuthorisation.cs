using System.Reflection;
using System.Security.Claims;
using MedApp.API.Common.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.UnitTests.Api.Controllers;

/// <summary>
/// Whether a controller action's own [Authorize] attributes (the controller's and the action's), with the API's
/// policies, let a user in: the same check the authorisation middleware makes.
/// </summary>
public static class ControllerAuthorisation
{
    public static async Task<bool> MayCallAsync<TController>(string action, ClaimsPrincipal user)
    {
        await using var services = new ServiceCollection().AddLogging().AddApi(new ConfigurationBuilder().Build()).BuildServiceProvider();
        var method = typeof(TController).GetMethod(action)
                     ?? throw new ArgumentException($"{typeof(TController).Name} has no action {action}.", nameof(action));
        var attributes = typeof(TController).GetCustomAttributes<AuthorizeAttribute>()
            .Concat(method.GetCustomAttributes<AuthorizeAttribute>());
        var policy = await AuthorizationPolicy.CombineAsync(services.GetRequiredService<IAuthorizationPolicyProvider>(), attributes);

        var result = await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, new DefaultHttpContext(), policy!);
        return result.Succeeded;
    }

    /// <summary>The public actions a controller declares itself.</summary>
    public static IEnumerable<string> Actions<TController>() =>
        typeof(TController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(m => m.Name);
}
