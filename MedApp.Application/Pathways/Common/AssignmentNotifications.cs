using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Common;

/// <summary>
/// The notification someone gets when they're assigned to a pathway: shared by <c>AssignPatientPathwayHandler</c>
/// and medapp-dev's demo data, so both say the same. Whether it's delivered is still up to their choices
/// (<see cref="NotificationPreferences"/>), which the sender checks.
/// </summary>
public static class AssignmentNotifications
{
    /// <summary>
    /// The notification for <paramref name="assigneeUserId"/>, assigned by <paramref name="actor"/>; null when they
    /// assigned themselves (they know already).
    /// </summary>
    public static Notification? For(PatientPathway pathway, Guid assigneeUserId, PathwayActor actor, DateTime nowUtc) =>
        assigneeUserId == actor.UserId
            ? null
            : new Notification
            {
                Id = Guid.NewGuid(),
                UserId = assigneeUserId,
                Kind = NotificationKind.PathwayAssigned,
                Title = pathway.Title,
                Message = $"{actor.Username} assigned you to this pathway.",
                PathwayId = pathway.Id,
                CreatedAtUtc = nowUtc
            };
}
