using MedApp.Application.Calendar.Common;
using MedApp.Domain.Clinics;
using MedApp.Domain.Notifications;

namespace MedApp.Application.Clinics.Common;

/// <summary>
/// What to tell a clinician when someone else cancels a clinic session of theirs (centre plan decision 126): one
/// notification for the session, with a fixed title, naming who, the room and when (admin-written names and UK time,
/// never the reason, a patient or how many were booked), linking to the Calendar that day. Its booked appointments are
/// cancelled with it, and the clinician isn't told of each one as well.
/// </summary>
public static class ClinicNotifications
{
    public const string CancelledTitle = "Clinic session cancelled";

    /// <summary>After cancelling a session: its clinician, unless they cancelled it themselves.</summary>
    public static IReadOnlyList<Notification> AfterCancelling(ClinicSession session, CalendarActor actor, DateTime nowUtc)
    {
        if (session.StaffUserId == actor.UserId)
            return [];

        var where = session.Room is { } room ? $" in {room.Name}" : string.Empty;
        return
        [
            new Notification
            {
                Id = Guid.NewGuid(),
                UserId = session.StaffUserId,
                Kind = NotificationKind.ClinicSessionCancelled,
                Title = CancelledTitle,
                Message = $"{actor.Username} cancelled your clinic session{where}, {UkTime.Describe(session.StartUtc)}.",
                CalendarDate = session.Date,
                CreatedAtUtc = nowUtc
            }
        ];
    }
}
