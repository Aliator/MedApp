using MedApp.Application.Common.Teams;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.NoteDeletionRequests.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Pathways.NoteDeletionRequests.Responses;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;
using MediatR;

namespace MedApp.Application.Pathways.NoteDeletionRequests.Commands.DecideNoteDeletionRequest;

public sealed class DecideNoteDeletionRequestHandler(
    ITeamBoundary boundary,
    INoteDeletionRequestRepository requests,
    IPatientPathwayRepository pathways,
    IPathwayMonitor pathwayMonitor,
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
            throw new ConflictException(PathwayNotes.AlreadyDecidedMessage(deletionRequest));

        // A 409 for a restricted or anonymised record: either decision changes the note (deleted, or no longer waiting).
        var pathway = await pathways.GetForCorrectionOrThrowAsync(deletionRequest.PatientPathwayId, boundary, request.Actor.Teams, cancellationToken);
        var note = pathway.Notes.FirstOrDefault(n => n.Id == deletionRequest.NoteId)
                   ?? throw new NotFoundException($"Note '{deletionRequest.NoteId}' was not found on this pathway.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        IReadOnlyList<Notification> toTell;
        if (request.DeleteNote)
        {
            if (note.IsDeleted)
                throw new ConflictException(PathwayNotes.DeletedMessage);

            // The admin must have read the text they're deleting.
            if (note.Version != request.ExpectedVersion)
                throw new ConflictException(PathwayNotes.EditedSinceOpenedMessage);

            // The admin's reason, or with none, the requester's, with the admin's detail if they wrote one and the
            // requester's otherwise: a blank detail is "left out", so a requester's Other never loses the detail it needs.
            var (reason, detail) = request.Reason is null
                ? (deletionRequest.Reason, NoteDeletionReasons.CleanDetail(request.Detail) ?? deletionRequest.Detail)
                : (PathwayEnums.Parse<NoteDeletionReason>(request.Reason), NoteDeletionReasons.CleanDetail(request.Detail));

            var change = new PathwayChange(pathway, request.Actor, now);
            change.DeleteNote(note, reason, detail, deletionRequest.Id);
            deletionRequest.Close(NoteDeletionRequestStatus.Approved, request.Actor.UserId, request.Actor.Username, now);

            // The request shares the DbContext, so it's saved with the change.
            await pathways.SaveAsync(change, cancellationToken);
            await pathwayMonitor.NotifyAsync(change, cancellationToken);
            toTell = NoteDeletionNotifications.AfterDeleting(note, deletionRequest, request.Actor, now);
        }
        else
        {
            deletionRequest.Close(NoteDeletionRequestStatus.Declined, request.Actor.UserId, request.Actor.Username, now);
            if (note.PendingDeletionRequestId == deletionRequest.Id)
                note.PendingDeletionRequestId = null;

            // The note shares the DbContext, so it's saved with the request.
            await requests.SaveAsync(cancellationToken);
            toTell = NoteDeletionNotifications.AfterDeciding(deletionRequest, now) is { } told ? [told] : [];
        }

        foreach (var notification in toTell)
            await notifications.SendAsync(notification, cancellationToken);

        var usernames = await identity.GetUsernamesByIdAsync(
            NoteDeletionRequestMapper.UserIds(deletionRequest, pathway), cancellationToken);
        return NoteDeletionRequestMapper.ToResponse(deletionRequest, pathway, pathway.Patient, usernames);
    }
}
