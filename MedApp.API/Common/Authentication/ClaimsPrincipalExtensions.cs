using System.Security.Claims;
using MedApp.Application.Procedures.Common;

namespace MedApp.API.Common.Authentication;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The signed-in user's ID, from the session's claims.</summary>
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? throw new InvalidOperationException("The user has no ID claim."));

    /// <summary>
    /// The ID of the session the request came with (<see cref="SessionAuthenticationDefaults.SessionIdClaim"/>), or
    /// null if it has none.
    /// </summary>
    public static Guid? GetSessionId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(SessionAuthenticationDefaults.SessionIdClaim), out var id) ? id : null;

    /// <summary>
    /// Whether the signed-in user must change the password an admin gave them before doing anything else (their
    /// session's <see cref="SessionAuthenticationDefaults.MustChangePasswordClaim"/>).
    /// </summary>
    public static bool MustChangePassword(this ClaimsPrincipal user) =>
        user.HasClaim(SessionAuthenticationDefaults.MustChangePasswordClaim, "true");

    /// <summary>The signed-in user, as procedure changes record them.</summary>
    public static ProcedureActor ToProcedureActor(this ClaimsPrincipal user) =>
        new(user.GetUserId(), user.Identity?.Name ?? string.Empty);
}
