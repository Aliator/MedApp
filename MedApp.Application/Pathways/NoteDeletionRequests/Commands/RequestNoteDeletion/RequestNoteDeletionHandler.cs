using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.NoteDeletionRequests.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Domain.Pathways.PatientPathways;
using MediatR;

namespace MedApp.Application.Pathways.NoteDeletionRequests.Commands.RequestNoteDeletion;

public sealed class RequestNoteDeletionHandler(
    IPatientPathwayRepository pathways,
    INoteDeletionRequestRepository requests,
    IIdentityReadService identity,
    INotificationSender notifications,
    TimeProvider timeProvider)
    : IRequestHandler<RequestNoteDeletionCommand, PatientPathwayNoteResponse>
{
    public async Task<PatientPathwayNoteResponse> Handle(RequestNoteDeletionCommand request, CancellationToken cancellationToken)
    {
        // A 409 for a restricted or anonymised record: a request is the first step of a change to it.
        var pathway = await pathways.GetForUpdateOrThrowAsync(request.PathwayId, cancellationToken);
        var note = pathway.Notes.FirstOrDefault(n => n.Id == request.NoteId)
                   ?? throw new NotFoundException($"Note '{request.NoteId}' was not found on this pathway.");

        if (note.IsDeleted)
            throw new ConflictException(PathwayNotes.DeletedMessage);

        if (note.PendingDeletionRequestId is { } pendingId)
            throw new ConflictException(
                PathwayNotes.AlreadyRequestedMessage(await requests.GetByIdAsync(pendingId, cancellationToken)));

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var deletionRequest = new PathwayNoteDeletionRequest
        {
            Id = Guid.NewGuid(),
            NoteId = note.Id,
            PatientPathwayId = pathway.Id,
            PatientId = pathway.PatientId,
            RequestedByUserId = request.Actor.UserId,
            RequestedByUsername = request.Actor.Username,
            // The validator has checked the reason's name.
            Reason = PathwayEnums.Parse<NoteDeletionReason>(request.Reason),
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
        return PathwayNoteMapper.ToResponse(
            note,
            usernames,
            request.Actor.UserId,
            canChange: PathwayWorkflow.CanChangeNotes(pathway),
            recordCanChange: true,
            viewerIsAdmin: request.Actor.IsAdmin);
    }
}
