using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace MedApp.API.Fhir;

/// <summary>
/// Answers a refused FHIR request with an OperationOutcome: 401 (<c>login</c>) when nobody is signed in, 403
/// (<c>forbidden</c>) when they lack the role. Every other path is refused as before, by the default handler.
/// </summary>
public sealed class FhirAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (!IsFhir(context.Request.Path) || authorizeResult.Succeeded)
        {
            await _default.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        if (authorizeResult.Challenged)
            await FhirJson.WriteAsync(
                context.Response,
                FhirJson.Outcome("login", "Sign in to use the FHIR API."),
                StatusCodes.Status401Unauthorized);
        else
            await FhirJson.WriteAsync(
                context.Response,
                FhirJson.Outcome("forbidden", "You don't have access to this resource."),
                StatusCodes.Status403Forbidden);
    }

    /// <summary>Whether the request is for the FHIR API (<c>/api/fhir</c>).</summary>
    public static bool IsFhir(PathString path) => path.StartsWithSegments("/api/fhir", StringComparison.OrdinalIgnoreCase);
}
