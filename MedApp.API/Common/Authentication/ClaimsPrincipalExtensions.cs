using System.Security.Claims;
using MedApp.Application.Procedures.Common;

namespace MedApp.API.Common.Authentication;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The signed-in user's ID, from the session's claims.</summary>
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? throw new InvalidOperationException("The user has no ID claim."));

    /// <summary>The signed-in user, as procedure changes record them.</summary>
    public static ProcedureActor ToProcedureActor(this ClaimsPrincipal user) =>
        new(user.GetUserId(), user.Identity?.Name ?? string.Empty);
}
