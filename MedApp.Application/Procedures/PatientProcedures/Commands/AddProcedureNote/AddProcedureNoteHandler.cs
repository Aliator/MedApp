using MedApp.Application.Common.Exceptions;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.AddProcedureNote;

public sealed class AddProcedureNoteHandler(
    IPatientProcedureRepository repository,
    IProcedureMonitor procedureMonitor,
    TimeProvider timeProvider)
    : IRequestHandler<AddProcedureNoteCommand, PatientProcedureNoteResponse>
{
    public async Task<PatientProcedureNoteResponse> Handle(AddProcedureNoteCommand request, CancellationToken cancellationToken)
    {
        // A 409 for a restricted or anonymised record, as for every change to a procedure.
        var procedure = await repository.GetForUpdateOrThrowAsync(request.ProcedureId, cancellationToken);

        if (ProcedureWorkflow.WhyNotesCantChange(procedure) is { } reason)
            throw new ConflictException(reason);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new ProcedureChange(procedure, request.Actor, now);
        var note = change.AddNote(request.Text);

        await repository.SaveAsync(change, cancellationToken);
        await procedureMonitor.NotifyAsync(change, cancellationToken);

        var usernames = new Dictionary<Guid, string> { [request.Actor.UserId] = request.Actor.Username };
        return ProcedureNoteMapper.ToResponse(note, usernames, request.Actor.UserId, canChange: true);
    }
}
