using MedApp.API.Fhir;
using MedApp.Application.Common.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Common.Authentication;

/// <summary>
/// Answers refused requests: the FHIR API with OperationOutcomes (<see cref="FhirAuthorizationResultHandler"/>); a
/// signed-in user without a permission an endpoint needs with a 403 <see cref="ProblemDetails"/> whose <c>code</c> is
/// <c>permission_missing</c>, naming the permission and saying what it allows, so the web app can tell the person and
/// hide what they can no longer do; everything else as before.
/// </summary>
public sealed class ApiAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    public const string PermissionMissingCode = "permission_missing";

    private const string ProblemJson = "application/problem+json";

    private readonly FhirAuthorizationResultHandler _fhir = new();
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (FhirAuthorizationResultHandler.IsFhir(context.Request.Path))
        {
            await _fhir.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        if (authorizeResult.Forbidden
            && authorizeResult.AuthorizationFailure?.FailedRequirements.OfType<PermissionRequirement>().FirstOrDefault() is { } missing)
        {
            var permission = Permissions.Describe(missing.Permission);
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Permission needed",
                Detail = $"Your roles don't allow this. It needs the permission to: {permission.Description}",
                Extensions =
                {
                    ["code"] = PermissionMissingCode,
                    ["permission"] = permission.Name,
                },
            };

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(problem, options: null, contentType: ProblemJson);
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
