using System.Security.Claims;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Pathways.Common;

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

    /// <summary>
    /// Whether the signed-in user must set up two-factor authentication before doing anything else (their session's
    /// <see cref="SessionAuthenticationDefaults.MustSetUpTwoFactorClaim"/>).
    /// </summary>
    public static bool MustSetUpTwoFactor(this ClaimsPrincipal user) =>
        user.HasClaim(SessionAuthenticationDefaults.MustSetUpTwoFactorClaim, "true");

    /// <summary>
    /// What the signed-in user's roles allow, from the session's <see cref="SessionAuthenticationDefaults.PermissionClaim"/>
    /// claims. Names code no longer knows are left out.
    /// </summary>
    public static IReadOnlySet<Permission> GetPermissions(this ClaimsPrincipal user) =>
        user.FindAll(SessionAuthenticationDefaults.PermissionClaim)
            .Select(claim => Permissions.TryParse(claim.Value, out var permission) ? permission : (Permission?)null)
            .OfType<Permission>()
            .ToHashSet();

    /// <summary>Whether the signed-in user's roles grant this permission.</summary>
    public static bool HasPermission(this ClaimsPrincipal user, Permission permission) =>
        user.HasClaim(SessionAuthenticationDefaults.PermissionClaim, permission.ToString());

    /// <summary>The teams the signed-in user belongs to, from the session's <see cref="SessionAuthenticationDefaults.TeamClaim"/> claims.</summary>
    public static IReadOnlyList<Guid> GetTeamIds(this ClaimsPrincipal user) =>
        user.FindAll(SessionAuthenticationDefaults.TeamClaim)
            .Select(claim => Guid.TryParse(claim.Value, out var id) ? id : (Guid?)null)
            .OfType<Guid>()
            .Distinct()
            .ToList();

    /// <summary>
    /// The signed-in user's team walls (<see cref="TeamScope"/>): their teams, and whether they see every team's records
    /// (<see cref="Permission.ViewAllTeams"/>). Never the system's.
    /// </summary>
    public static TeamScope ToTeamScope(this ClaimsPrincipal user) =>
        TeamScope.For(user.GetUserId(), user.GetTeamIds(), user.GetPermissions());

    /// <summary>The signed-in user, as pathway changes record them, with what their roles allow and their team walls.</summary>
    public static PathwayActor ToPathwayActor(this ClaimsPrincipal user) =>
        new(user.GetUserId(), user.Identity?.Name ?? string.Empty, user.GetPermissions(), user.ToTeamScope());

    /// <summary>
    /// The signed-in user, for calendar changes: their own calendar, or anyone's with
    /// <see cref="Permission.ManageAnyCalendar"/>; meetings with <see cref="Permission.ArrangeMeetings"/>.
    /// </summary>
    public static CalendarActor ToCalendarActor(this ClaimsPrincipal user) =>
        new(user.GetUserId(), user.Identity?.Name ?? string.Empty, user.GetPermissions(), user.ToTeamScope());
}
