using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.NoteDeletionRequests.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.NoteDeletionRequests.Responses;
using MedApp.Domain.Procedures.PatientProcedures;
using MediatR;

namespace MedApp.Application.Procedures.NoteDeletionRequests.Commands.DecideNoteDeletionRequest;

public sealed class DecideNoteDeletionRequestHandler(
    INoteDeletionRequestRepository requests,
    IPatientProcedureRepository procedures,
    IProcedureMonitor procedureMonitor,
    INotificationSender notifications,
    IIdentityReadService identity,
    TimeProvider timeProvider)
    : IRequestHandler<DecideNoteDeletionRequestCommand, NoteDeletionRequestResponse>
{
    public async Task<NoteDeletionRequestResponse> Handle(DecideNoteDeletionRequestCommand request, CancellationToken cancellationToken)
    {
        var deletionRequest = await requests.GetForUpdateAsync(request.RequestId, cancellationToken)
                              ?? throw new NotFoundException($"Note deletion request '{request.RequestId}' was not found.");

        // Another admin got there first.
        if (!deletionRequest.IsPending)
            throw new ConflictException(ProcedureNotes.AlreadyDecidedMessage(deletionRequest));

        // A 409 for a restricted or anonymised record: either decision changes the note (deleted, or no longer waiting).
        var procedure = await procedures.GetForUpdateOrThrowAsync(deletionRequest.PatientProcedureId, cancellationToken);
        var note = procedure.Notes.FirstOrDefault(n => n.Id == deletionRequest.NoteId)
                   ?? throw new NotFoundException($"Note '{deletionRequest.NoteId}' was not found on this procedure.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (request.DeleteNote)
        {
            if (note.IsDeleted)
                throw new ConflictException(ProcedureNotes.DeletedMessage);

            var change = new ProcedureChange(procedure, request.Actor, now);
            change.DeleteNote(note, request.Reason?.Trim() ?? deletionRequest.Reason, deletionRequest.Id);
            deletionRequest.Close(NoteDeletionRequestStatus.Approved, request.Actor.UserId, request.Actor.Username, now);

            // The request shares the DbContext, so it's saved with the change.
            await procedures.SaveAsync(change, cancellationToken);
            await procedureMonitor.NotifyAsync(change, cancellationToken);
        }
        else
        {
            deletionRequest.Close(NoteDeletionRequestStatus.Declined, request.Actor.UserId, request.Actor.Username, now);
            if (note.PendingDeletionRequestId == deletionRequest.Id)
                note.PendingDeletionRequestId = null;

            // The note shares the DbContext, so it's saved with the request.
            await requests.SaveAsync(cancellationToken);
        }

        if (NoteDeletionNotifications.AfterDeciding(deletionRequest, now) is { } told)
            await notifications.SendAsync(told, cancellationToken);

        var usernames = await identity.GetUsernamesByIdAsync(
            NoteDeletionRequestMapper.UserIds(deletionRequest, procedure), cancellationToken);
        return NoteDeletionRequestMapper.ToResponse(deletionRequest, procedure, procedure.Patient, usernames);
    }
}
