using MedApp.Application.Common.Exceptions;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.NoteDeletionRequests.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Pathways.PatientPathways;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.DeletePathwayNote;

public sealed class DeletePathwayNoteHandler(
    IPatientPathwayRepository repository,
    INoteDeletionRequestRepository requests,
    IPathwayMonitor pathwayMonitor,
    INotificationSender notifications,
    TimeProvider timeProvider)
    : IRequestHandler<DeletePathwayNoteCommand>
{
    public async Task Handle(DeletePathwayNoteCommand request, CancellationToken cancellationToken)
    {
        // A 409 for a restricted or anonymised record, as for every change to a pathway. Any status otherwise: deleting
        // is a correction (a note on the wrong patient), as an admin can delete a file at any time.
        var pathway = await repository.GetForUpdateOrThrowAsync(request.PathwayId, cancellationToken);
        var note = pathway.Notes.FirstOrDefault(n => n.Id == request.NoteId)
                   ?? throw new NotFoundException($"Note '{request.NoteId}' was not found on this pathway.");

        if (note.IsDeleted)
            throw new ConflictException(PathwayNotes.DeletedMessage);

        // The admin must have read the text they're deleting.
        if (note.Version != request.ExpectedVersion)
            throw new ConflictException(PathwayNotes.EditedSinceOpenedMessage);

        // A request waiting for the note is approved by this, in the same save (the request shares the DbContext).
        var pending = note.PendingDeletionRequestId is { } pendingId
            ? await requests.GetForUpdateAsync(pendingId, cancellationToken)
            : null;
        if (pending is { IsPending: false })
            pending = null;

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new PathwayChange(pathway, request.Actor, now);
        // The validator has checked the reason's name.
        change.DeleteNote(note, PathwayEnums.Parse<NoteDeletionReason>(request.Reason), NoteDeletionReasons.CleanDetail(request.Detail), pending?.Id);
        pending?.Close(NoteDeletionRequestStatus.Approved, request.Actor.UserId, request.Actor.Username, now);

        await repository.SaveAsync(change, cancellationToken);
        await pathwayMonitor.NotifyAsync(change, cancellationToken);

        foreach (var told in NoteDeletionNotifications.AfterDeleting(note, pending, request.Actor, now))
            await notifications.SendAsync(told, cancellationToken);
    }
}
