using MedApp.Application.Common.Identity;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Queries.GetPatientPathwayById;

/// <summary>
/// A pathway in full, for <paramref name="ViewerId"/> (the signed-in user: their own notes are the ones they can edit)
/// with <paramref name="ViewerPermissions"/> (what their roles allow, from the session), so the response's
/// <c>Can...</c> flags say what this viewer can do. Null permissions mean no viewer is known (see
/// <c>PathwayMapper.ToResponse</c>).
/// </summary>
public sealed record GetPatientPathwayByIdQuery(
    Guid Id,
    Guid? ViewerId = null,
    IReadOnlySet<Permission>? ViewerPermissions = null) : IRequest<PatientPathwayResponse>
{
    /// <summary>Queries compare their permissions by content, so two with the same set are equal.</summary>
    public bool Equals(GetPatientPathwayByIdQuery? other) =>
        other is not null
        && Id == other.Id
        && ViewerId == other.ViewerId
        && (ViewerPermissions is null
            ? other.ViewerPermissions is null
            : other.ViewerPermissions is not null && ViewerPermissions.SetEquals(other.ViewerPermissions));

    public override int GetHashCode() => HashCode.Combine(Id, ViewerId, ViewerPermissions?.Count);
}
