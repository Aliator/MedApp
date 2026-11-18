using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.EditPathwayNote;

public sealed class EditPathwayNoteHandler(
    IPatientPathwayRepository repository,
    IPathwayMonitor pathwayMonitor,
    TimeProvider timeProvider)
    : IRequestHandler<EditPathwayNoteCommand, PatientPathwayNoteResponse>
{
    public async Task<PatientPathwayNoteResponse> Handle(EditPathwayNoteCommand request, CancellationToken cancellationToken)
    {
        // A 409 for a restricted or anonymised record, as for every change to a pathway.
        var pathway = await repository.GetForUpdateOrThrowAsync(request.PathwayId, cancellationToken);
        var note = pathway.Notes.FirstOrDefault(n => n.Id == request.NoteId)
                   ?? throw new NotFoundException($"Note '{request.NoteId}' was not found on this pathway.");

        // An admin deleted it: it's kept, but nobody can change it.
        if (note.IsDeleted)
            throw new ConflictException(PathwayNotes.DeletedMessage);

        // Only the author, admins included: what someone wrote in the record stays theirs.
        if (note.AuthorUserId is null || note.AuthorUserId != request.Actor.UserId)
            throw new ForbiddenException(PathwayNotes.OnlyTheAuthorMessage);

        if (PathwayWorkflow.WhyNotesCantChange(pathway) is { } reason)
            throw new ConflictException(reason);

        if (note.Version != request.ExpectedVersion)
            throw new ConflictException(PathwayNotes.ChangedMeanwhileMessage);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new PathwayChange(pathway, request.Actor, now);
        change.EditNote(note, request.Text);

        if (change.HasChanges)
        {
            await repository.SaveAsync(change, cancellationToken);
            await pathwayMonitor.NotifyAsync(change, cancellationToken);
        }

        var usernames = new Dictionary<Guid, string> { [request.Actor.UserId] = request.Actor.Username };
        return PathwayNoteMapper.ToResponse(note, usernames, request.Actor.UserId, canChange: true, viewerCanCorrectRecords: request.Actor.Has(Permission.CorrectRecords));
    }
}
