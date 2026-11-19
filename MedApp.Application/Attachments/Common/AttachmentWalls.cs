using MedApp.Application.Common.Teams;
using MedApp.Domain.Attachments;

namespace MedApp.Application.Attachments.Common;

/// <summary>
/// The team walls on files (section 3.2 of <c>docs/multi-clinic-plan.md</c>): a file follows its pathway, or its own team
/// when it has none (a referral's letter, a patient's own file), as <see cref="TeamAccess.ByPathway{T}"/> does in SQL.
/// Behind the viewer's wall it's a 404 with the words of a missing file, recorded (<c>TeamBoundaryRefused</c>).
/// </summary>
public static class AttachmentWalls
{
    /// <summary>The 404's words for a file that doesn't exist, and for one behind the viewer's wall (the same).</summary>
    public static string NotFoundMessage(Guid id) => $"File '{id}' was not found.";

    /// <summary>Whether the viewer sees the file: their team's, or on a pathway they see in full (or they see every team's).</summary>
    public static async Task<bool> CanSeeAsync(Attachment attachment, ITeamBoundary boundary, TeamScope scope, CancellationToken ct) =>
        TeamAccess.CanSeeTeam(attachment.TeamId, scope)
        || (attachment.PathwayId is { } pathwayId && await boundary.GetPathwayAccessAsync(pathwayId, scope, ct) is not null);

    /// <summary>The file seen by the viewer, or a 404 (recorded) when it's behind their wall.</summary>
    public static async Task EnsureVisibleAsync(Attachment attachment, ITeamBoundary boundary, TeamScope scope, CancellationToken ct)
    {
        if (!await CanSeeAsync(attachment, boundary, scope, ct))
            throw await boundary.RefuseAsync(TeamWalls.AttachmentTarget, attachment.Id, NotFoundMessage(attachment.Id), ct);
    }
}
