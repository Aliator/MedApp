using MedApp.Application.Common.Exceptions;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.EditProcedureNote;

public sealed class EditProcedureNoteHandler(
    IPatientProcedureRepository repository,
    IProcedureMonitor procedureMonitor,
    TimeProvider timeProvider)
    : IRequestHandler<EditProcedureNoteCommand, PatientProcedureNoteResponse>
{
    public async Task<PatientProcedureNoteResponse> Handle(EditProcedureNoteCommand request, CancellationToken cancellationToken)
    {
        // A 409 for a restricted or anonymised record, as for every change to a procedure.
        var procedure = await repository.GetForUpdateOrThrowAsync(request.ProcedureId, cancellationToken);
        var note = procedure.Notes.FirstOrDefault(n => n.Id == request.NoteId)
                   ?? throw new NotFoundException($"Note '{request.NoteId}' was not found on this procedure.");

        // Only the author, admins included: what someone wrote in the record stays theirs.
        if (note.AuthorUserId is null || note.AuthorUserId != request.Actor.UserId)
            throw new ForbiddenException(ProcedureNotes.OnlyTheAuthorMessage);

        if (ProcedureWorkflow.WhyNotesCantChange(procedure) is { } reason)
            throw new ConflictException(reason);

        if (note.Version != request.ExpectedVersion)
            throw new ConflictException(ProcedureNotes.ChangedMeanwhileMessage);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new ProcedureChange(procedure, request.Actor, now);
        change.EditNote(note, request.Text);

        if (change.HasChanges)
        {
            await repository.SaveAsync(change, cancellationToken);
            await procedureMonitor.NotifyAsync(change, cancellationToken);
        }

        var usernames = new Dictionary<Guid, string> { [request.Actor.UserId] = request.Actor.Username };
        return ProcedureNoteMapper.ToResponse(note, usernames, request.Actor.UserId, canChange: true);
    }
}
