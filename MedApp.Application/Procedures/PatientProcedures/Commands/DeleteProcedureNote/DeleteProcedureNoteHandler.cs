using MedApp.Application.Common.Exceptions;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.NoteDeletionRequests.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.DeleteProcedureNote;

public sealed class DeleteProcedureNoteHandler(
    IPatientProcedureRepository repository,
    INoteDeletionRequestRepository requests,
    IProcedureMonitor procedureMonitor,
    INotificationSender notifications,
    TimeProvider timeProvider)
    : IRequestHandler<DeleteProcedureNoteCommand>
{
    public async Task Handle(DeleteProcedureNoteCommand request, CancellationToken cancellationToken)
    {
        // A 409 for a restricted or anonymised record, as for every change to a procedure. Any status otherwise: deleting
        // is a correction (a note on the wrong patient), as an admin can delete a file at any time.
        var procedure = await repository.GetForUpdateOrThrowAsync(request.ProcedureId, cancellationToken);
        var note = procedure.Notes.FirstOrDefault(n => n.Id == request.NoteId)
                   ?? throw new NotFoundException($"Note '{request.NoteId}' was not found on this procedure.");

        if (note.IsDeleted)
            throw new ConflictException(ProcedureNotes.DeletedMessage);

        // A request waiting for the note is approved by this, in the same save (the request shares the DbContext).
        var pending = note.PendingDeletionRequestId is { } pendingId
            ? await requests.GetForUpdateAsync(pendingId, cancellationToken)
            : null;
        if (pending is { IsPending: false })
            pending = null;

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new ProcedureChange(procedure, request.Actor, now);
        change.DeleteNote(note, request.Reason.Trim(), pending?.Id);
        pending?.Close(NoteDeletionRequestStatus.Approved, request.Actor.UserId, request.Actor.Username, now);

        await repository.SaveAsync(change, cancellationToken);
        await procedureMonitor.NotifyAsync(change, cancellationToken);

        if (pending is not null && NoteDeletionNotifications.AfterDeciding(pending, now) is { } told)
            await notifications.SendAsync(told, cancellationToken);
    }
}
