using MedApp.Application.Pathways.Common;
using MedApp.Domain.Mdt;
using MedApp.Domain.Notifications;

namespace MedApp.Application.Mdt.Common;

/// <summary>
/// What a pathway's assignees are told when an MDT's outcome is recorded on it
/// (<see cref="NotificationKind.MdtOutcomeRecorded"/>), so someone acts on it (hazard H-58). A fixed title, and a message
/// from the recorder's username and the specialty (an admin-written name): never the outcome, the question or the
/// patient. Whoever recorded it isn't told.
/// </summary>
public static class MdtNotifications
{
    public const string OutcomeTitle = "MDT outcome recorded";

    public static IReadOnlyList<Notification> OutcomeRecorded(MdtItem item, PathwayActor actor, DateTime nowUtc) =>
        item.Pathway.Assignments
            .Select(a => a.UserId)
            .Where(id => id != actor.UserId)
            .Distinct()
            .Select(userId => new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Kind = NotificationKind.MdtOutcomeRecorded,
                Title = OutcomeTitle,
                Message = Message(actor.Username, item.Specialty?.Name),
                PathwayId = item.PathwayId,
                CreatedAtUtc = nowUtc
            })
            .ToList();

    /// <summary>"k.ashford recorded the Dermatology MDT's outcome on a pathway you're assigned to."</summary>
    public static string Message(string username, string? specialty) =>
        $"{username} recorded the {(string.IsNullOrEmpty(specialty) ? "team" : specialty)} MDT's outcome on a pathway you're assigned to.";
}
