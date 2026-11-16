using MedApp.Domain.Notifications;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.Common;

/// <summary>
/// The notification someone gets when they're assigned to a procedure: shared by <c>AssignPatientProcedureHandler</c>
/// and medapp-dev's demo data, so both say the same. Whether it's delivered is still up to their choices
/// (<see cref="NotificationPreferences"/>), which the sender checks.
/// </summary>
public static class AssignmentNotifications
{
    /// <summary>
    /// The notification for <paramref name="assigneeUserId"/>, assigned by <paramref name="actor"/>; null when they
    /// assigned themselves (they know already).
    /// </summary>
    public static Notification? For(PatientProcedure procedure, Guid assigneeUserId, ProcedureActor actor, DateTime nowUtc) =>
        assigneeUserId == actor.UserId
            ? null
            : new Notification
            {
                Id = Guid.NewGuid(),
                UserId = assigneeUserId,
                Kind = NotificationKind.ProcedureAssigned,
                Title = procedure.Title,
                Message = $"{actor.Username} assigned you to this procedure.",
                ProcedureId = procedure.Id,
                CreatedAtUtc = nowUtc
            };
}
