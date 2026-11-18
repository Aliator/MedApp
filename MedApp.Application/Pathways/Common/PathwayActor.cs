using MedApp.Application.Common.Identity;

namespace MedApp.Application.Pathways.Common;

/// <summary>
/// The signed-in staff member making a change, as the audit log records them, and what their roles allow
/// (<paramref name="Permissions"/>, from the session), for rules a handler checks itself and the <c>Can...</c> flags of
/// the response they get back. Endpoints check their own permission before the handler runs.
/// </summary>
public sealed record PathwayActor(Guid UserId, string Username, IReadOnlySet<Permission>? Permissions = null)
{
    /// <summary>Whether their roles grant this permission.</summary>
    public bool Has(Permission permission) => Permissions?.Contains(permission) == true;

    /// <summary>Records compare their permissions by content, so two actors with the same set are equal.</summary>
    public bool Equals(PathwayActor? other) =>
        other is not null
        && UserId == other.UserId
        && Username == other.Username
        && (Permissions ?? Empty).SetEquals(other.Permissions ?? Empty);

    public override int GetHashCode() => HashCode.Combine(UserId, Username, Permissions?.Count ?? 0);

    private static readonly HashSet<Permission> Empty = [];
}
