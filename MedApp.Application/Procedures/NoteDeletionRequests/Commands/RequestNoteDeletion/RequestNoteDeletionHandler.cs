using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.NoteDeletionRequests.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.Domain.Procedures.PatientProcedures;
using MediatR;

namespace MedApp.Application.Procedures.NoteDeletionRequests.Commands.RequestNoteDeletion;

public sealed class RequestNoteDeletionHandler(
    IPatientProcedureRepository procedures,
    INoteDeletionRequestRepository requests,
    IIdentityReadService identity,
    INotificationSender notifications,
    TimeProvider timeProvider)
    : IRequestHandler<RequestNoteDeletionCommand, PatientProcedureNoteResponse>
{
    public async Task<PatientProcedureNoteResponse> Handle(RequestNoteDeletionCommand request, CancellationToken cancellationToken)
    {
        // A 409 for a restricted or anonymised record: a request is the first step of a change to it.
        var procedure = await procedures.GetForUpdateOrThrowAsync(request.ProcedureId, cancellationToken);
        var note = procedure.Notes.FirstOrDefault(n => n.Id == request.NoteId)
                   ?? throw new NotFoundException($"Note '{request.NoteId}' was not found on this procedure.");

        if (note.IsDeleted)
            throw new ConflictException(ProcedureNotes.DeletedMessage);

        if (note.PendingDeletionRequestId is { } pendingId)
            throw new ConflictException(
                ProcedureNotes.AlreadyRequestedMessage(await requests.GetByIdAsync(pendingId, cancellationToken)));

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var deletionRequest = new ProcedureNoteDeletionRequest
        {
            Id = Guid.NewGuid(),
            NoteId = note.Id,
            PatientProcedureId = procedure.Id,
            PatientId = procedure.PatientId,
            RequestedByUserId = request.Actor.UserId,
            RequestedByUsername = request.Actor.Username,
            // The validator has checked the reason's name.
            Reason = ProcedureEnums.Parse<NoteDeletionReason>(request.Reason),
            Detail = NoteDeletionReasons.CleanDetail(request.Detail),
            RequestedAtUtc = now
        };

        // Saved with the request: the note's row version stops two requests for it being made at once.
        note.PendingDeletionRequestId = deletionRequest.Id;
        await requests.AddAsync(deletionRequest, cancellationToken);

        var admins = await identity.GetAdminIdsAsync(cancellationToken);
        foreach (var notification in NoteDeletionNotifications.AfterRequesting(deletionRequest, admins, now))
            await notifications.SendAsync(notification, cancellationToken);

        var usernames = await identity.GetUsernamesByIdAsync(
            note.AuthorUserId is { } author ? [author] : [], cancellationToken);
        return ProcedureNoteMapper.ToResponse(
            note,
            usernames,
            request.Actor.UserId,
            canChange: ProcedureWorkflow.CanChangeNotes(procedure),
            recordCanChange: true,
            viewerIsAdmin: request.Actor.IsAdmin);
    }
}
