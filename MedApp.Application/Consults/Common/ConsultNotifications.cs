using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Domain.Consults;
using MedApp.Domain.Notifications;

namespace MedApp.Application.Consults.Common;

/// <summary>
/// Who is told of a consult, after its save (hazard H-75, a reply missed): a fixed title, and who and which team (an
/// admin-written name); never the question, the reply or the patient. Whoever made the change isn't told.
/// <list type="bullet">
/// <item><b>Sent:</b> the asked team's members who work pathways (<see cref="NotificationKind.ConsultRequested"/>).</item>
/// <item><b>Replied:</b> whoever sent it and the pathway's assignees (<see cref="NotificationKind.ConsultReplied"/>).</item>
/// </list>
/// </summary>
public interface IConsultNotifications
{
    /// <summary>After a consult was sent (<see cref="ConsultChange.Requested"/>) and saved.</summary>
    Task RequestedAsync(ConsultChange change, string askingTeamName, CancellationToken ct);

    /// <summary>After a reply (<see cref="ConsultChange.Replied"/>) was saved; the consult's pathway is loaded with its assignees.</summary>
    Task RepliedAsync(ConsultChange change, string askedTeamName, CancellationToken ct);
}

/// <inheritdoc />
public sealed class ConsultNotifications(
    ITeamRepository teams,
    IPermissionHolders holders,
    INotificationSender sender)
    : IConsultNotifications
{
    public const string RequestedTitle = "Consult requested";
    public const string RepliedTitle = "Consult replied";

    /// <summary>"n.thornbury from Dermatology asked your team for a consult."</summary>
    public static string RequestedMessage(string username, string teamName) =>
        $"{username} from {teamName} asked your team for a consult.";

    /// <summary>"r.aldous from Lab replied to your team's consult."</summary>
    public static string RepliedMessage(string username, string teamName) =>
        $"{username} from {teamName} replied to your team\u2019s consult.";

    public async Task RequestedAsync(ConsultChange change, string askingTeamName, CancellationToken ct)
    {
        var (consult, actorId, nowUtc) = (change.Consult, change.Actor.UserId, change.NowUtc);
        var members = await teams.GetMemberIdsAsync(consult.AskedTeamId, ct);
        var working = await holders.WhoHoldsAsync(members, Permission.WorkPathways, ct);
        var message = RequestedMessage(change.Actor.Username, askingTeamName);
        foreach (var userId in members.Where(id => working.Contains(id) && id != actorId).Distinct())
            await sender.SendAsync(Notify(userId, NotificationKind.ConsultRequested, RequestedTitle, message, consult, nowUtc), ct);
    }

    public async Task RepliedAsync(ConsultChange change, string askedTeamName, CancellationToken ct)
    {
        var (consult, actorId, nowUtc) = (change.Consult, change.Actor.UserId, change.NowUtc);
        var message = RepliedMessage(change.Actor.Username, askedTeamName);
        var told = new[] { consult.RequestedByUserId }
            .Concat(consult.Pathway?.Assignments.Select(a => a.UserId) ?? [])
            .Where(id => id != actorId)
            .Distinct();
        foreach (var userId in told)
            await sender.SendAsync(Notify(userId, NotificationKind.ConsultReplied, RepliedTitle, message, consult, nowUtc), ct);
    }

    private static Notification Notify(
        Guid userId, NotificationKind kind, string title, string message, Consult consult, DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Kind = kind,
        Title = title,
        Message = message,
        PathwayId = consult.PathwayId,
        CreatedAtUtc = nowUtc
    };
}
