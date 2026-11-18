using MedApp.Application.Procedures.Common;
using MedApp.Domain.Notifications;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.NoteDeletionRequests.Common;

/// <summary>
/// What to tell people about deleting a procedure's note: every admin (but the requester) when a request is made; the
/// requester when an admin decides it (unless they decided it themselves); and the note's author when it's deleted
/// (unless they deleted it, or asked for it, themselves). Titles are fixed phrases (<see cref="TitleOf"/>), and messages
/// hold only usernames: never the note, the reason or the patient.
/// </summary>
public static class NoteDeletionNotifications
{
    public static string TitleOf(NotificationKind kind) => kind switch
    {
        NotificationKind.NoteDeletionRequested => "Note deletion requested",
        NotificationKind.NoteDeletionApproved => "Note deleted",
        NotificationKind.NoteDeletionDeclined => "Note kept",
        NotificationKind.AuthorsNoteDeleted => "Your note was deleted",
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

    /// <summary>
    /// After an admin deletes a note (directly, or from a request): its author is told, linking to the procedure, so
    /// someone who knows what it said can add it to the right record, or say it shouldn't have gone. Not when the author
    /// deleted it themselves, isn't known (a note moved from the old single field), or asked for it to be deleted (the
    /// requester is told already).
    /// </summary>
    public static Notification? ToAuthor(PatientProcedureNote note, ProcedureNoteDeletionRequest? request, ProcedureActor admin, DateTime nowUtc)
    {
        if (note.AuthorUserId is not { } author || author == admin.UserId || author == request?.RequestedByUserId)
            return null;

        return new Notification
        {
            Id = Guid.NewGuid(),
            UserId = author,
            Kind = NotificationKind.AuthorsNoteDeleted,
            Title = TitleOf(NotificationKind.AuthorsNoteDeleted),
            Message = $"{admin.Username} deleted a note you wrote on a procedure.",
            ProcedureId = note.PatientProcedureId,
            CreatedAtUtc = nowUtc
        };
    }

    /// <summary>Everyone to tell after an admin deletes a note: the requester (if it was asked for) and the author.</summary>
    public static IReadOnlyList<Notification> AfterDeleting(
        PatientProcedureNote note, ProcedureNoteDeletionRequest? request, ProcedureActor admin, DateTime nowUtc) =>
        new[] { request is null ? null : AfterDeciding(request, nowUtc), ToAuthor(note, request, admin, nowUtc) }
            .OfType<Notification>()
            .ToList();
}
