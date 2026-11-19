using MedApp.Application.Common.Teams;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.AddPathwayNote;

public sealed class AddPathwayNoteHandler(
    ITeamBoundary boundary,
    IPatientPathwayRepository repository,
    IPathwayMonitor pathwayMonitor,
    TimeProvider timeProvider)
    : IRequestHandler<AddPathwayNoteCommand, PatientPathwayNoteResponse>
{
    public async Task<PatientPathwayNoteResponse> Handle(AddPathwayNoteCommand request, CancellationToken cancellationToken)
    {
        // A 409 for a restricted or anonymised record, as for every change to a pathway.
        // Its own team, or the team working its current stage (the relay), adds and edits notes.
        var pathway = await repository.GetForNotesOrThrowAsync(request.PathwayId, boundary, request.Actor.Teams, cancellationToken);

        if (PathwayWorkflow.WhyNotesCantChange(pathway) is { } reason)
            throw new ConflictException(reason);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new PathwayChange(pathway, request.Actor, now);
        var note = change.AddNote(request.Text);

        await repository.SaveAsync(change, cancellationToken);
        await pathwayMonitor.NotifyAsync(change, cancellationToken);

        var usernames = new Dictionary<Guid, string> { [request.Actor.UserId] = request.Actor.Username };
        return PathwayNoteMapper.ToResponse(note, usernames, request.Actor.UserId, canChange: true, viewerPermissions: request.Actor.Permissions);
    }
}
