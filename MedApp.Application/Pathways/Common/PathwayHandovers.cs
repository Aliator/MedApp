using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Domain.Notifications;

namespace MedApp.Application.Pathways.Common;

/// <summary>
/// Tells a team a pathway was handed to it (MC7, the relay): after a change is saved, each receiving team's members who
/// work pathways (<see cref="Permission.WorkPathways"/>), but not whoever passed it, get <see cref="NotificationKind.PathwayHandedOver"/>.
/// A convenience: the team's New for us view, and pick-up, are the safety net (hazard H-67).
/// </summary>
public interface IPathwayHandovers
{
    Task NotifyAsync(PathwayChange change, CancellationToken ct);
}

/// <inheritdoc />
public sealed class PathwayHandovers(
    ITeamRepository teams,
    IPermissionHolders holders,
    INotificationSender sender)
    : IPathwayHandovers
{
    public const string Title = "Pathway handed over";

    /// <summary>The message: who passed it, never the patient or the stage.</summary>
    public static string Message(string username) => $"{username} passed a pathway to your team.";

    public async Task NotifyAsync(PathwayChange change, CancellationToken ct)
    {
        foreach (var teamId in change.Handovers.Select(h => h.ToTeamId).Distinct())
        {
            var members = await teams.GetMemberIdsAsync(teamId, ct);
            var working = await holders.WhoHoldsAsync(members, Permission.WorkPathways, ct);
            foreach (var userId in members.Where(id => working.Contains(id) && id != change.Actor.UserId))
                await sender.SendAsync(new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Kind = NotificationKind.PathwayHandedOver,
                    Title = Title,
                    Message = Message(change.Actor.Username),
                    PathwayId = change.Pathway.Id,
                    CreatedAtUtc = change.NowUtc
                }, ct);
        }
    }
}
