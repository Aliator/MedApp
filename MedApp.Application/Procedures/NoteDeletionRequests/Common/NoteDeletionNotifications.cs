using MedApp.Domain.Notifications;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.NoteDeletionRequests.Common;

/// <summary>
/// What to tell people about requests to delete a procedure's note: every admin (but the requester) when one is made,
/// and the requester when an admin decides it (unless they decided it themselves). Titles are fixed phrases
/// (<see cref="TitleOf"/>), and messages hold only usernames: never the note, the reason or the patient.
/// </summary>
public static class NoteDeletionNotifications
{
    public static string TitleOf(NotificationKind kind) => kind switch
    {
        NotificationKind.NoteDeletionRequested => "Note deletion requested",
        NotificationKind.NoteDeletionApproved => "Note deleted",
        NotificationKind.NoteDeletionDeclined => "Note kept",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    /// <summary>
    /// After a request is made: one to each admin but the requester, linking to the request (which opens on its
    /// procedure's page).
    /// </summary>
    public static IReadOnlyList<Notification> AfterRequesting(
        ProcedureNoteDeletionRequest request,
        IEnumerable<Guid> adminIds,
        DateTime nowUtc) =>
        adminIds
            .Distinct()
            .Where(id => id != request.RequestedByUserId)
            .Select(id => new Notification
            {
                Id = Guid.NewGuid(),
                UserId = id,
                Kind = NotificationKind.NoteDeletionRequested,
                Title = TitleOf(NotificationKind.NoteDeletionRequested),
                Message = $"{request.RequestedByUsername} asked to delete a note on a procedure.",
                ProcedureId = request.PatientProcedureId,
                NoteDeletionRequestId = request.Id,
                CreatedAtUtc = nowUtc
            })
            .ToList();

    /// <summary>
    /// After an admin decides a request (deleting the note, or keeping it): the requester is told, linking to the
    /// procedure. Nobody is told about their own decision, or about a request closed without one.
    /// </summary>
    public static Notification? AfterDeciding(ProcedureNoteDeletionRequest request, DateTime nowUtc)
    {
        var kind = request.Status switch
        {
            NoteDeletionRequestStatus.Approved => NotificationKind.NoteDeletionApproved,
            NoteDeletionRequestStatus.Declined => NotificationKind.NoteDeletionDeclined,
            _ => (NotificationKind?)null
        };
        if (kind is null || request.DecidedByUserId == request.RequestedByUserId)
            return null;

        var by = string.IsNullOrEmpty(request.DecidedByUsername) ? "An admin" : request.DecidedByUsername;
        return new Notification
        {
            Id = Guid.NewGuid(),
            UserId = request.RequestedByUserId,
            Kind = kind.Value,
            Title = TitleOf(kind.Value),
            Message = kind == NotificationKind.NoteDeletionApproved
                ? $"{by} deleted the note you asked to delete."
                : $"{by} kept the note you asked to delete.",
            ProcedureId = request.PatientProcedureId,
            CreatedAtUtc = nowUtc
        };
    }
}
