using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Queries.GetPatientPathwayById;

/// <summary>
/// A pathway in full, for <paramref name="ViewerId"/> (the signed-in user: their own notes are the ones they can edit)
/// with <paramref name="ViewerPermissions"/> (what their roles allow, from the session), so the response's
/// <c>Can...</c> flags say what this viewer can do. Null permissions mean no viewer is known (see
/// <c>PathwayMapper.ToResponse</c>). <paramref name="Scope"/> is the viewer's team walls (from the request; null
/// only outside one, which is unscoped): behind them the pathway is a 404, recorded.
/// </summary>
public sealed record GetPatientPathwayByIdQuery(
    Guid Id,
    Guid? ViewerId = null,
    IReadOnlySet<Permission>? ViewerPermissions = null,
    TeamScope? Scope = null) : IRequest<PatientPathwayResponse>
{
    /// <summary>The viewer's team walls: the request's, or the system's outside a request.</summary>
    public TeamScope Teams => Scope ?? TeamScope.System;

    /// <summary>Queries compare their permissions by content, so two with the same set are equal.</summary>
    public bool Equals(GetPatientPathwayByIdQuery? other) =>
        other is not null
        && Id == other.Id
        && ViewerId == other.ViewerId
        && Equals(Scope, other.Scope)
        && (ViewerPermissions is null
            ? other.ViewerPermissions is null
            : other.ViewerPermissions is not null && ViewerPermissions.SetEquals(other.ViewerPermissions));

    public override int GetHashCode() => HashCode.Combine(Id, ViewerId, ViewerPermissions?.Count);
}
