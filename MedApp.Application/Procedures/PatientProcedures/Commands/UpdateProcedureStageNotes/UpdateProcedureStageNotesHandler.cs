using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.UpdateProcedureStageNotes;

public sealed class UpdateProcedureStageNotesHandler(
    IPatientProcedureRepository repository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateProcedureStageNotesCommand, PatientProcedureResponse>
{
    public async Task<PatientProcedureResponse> Handle(UpdateProcedureStageNotesCommand request, CancellationToken cancellationToken)
    {
        var procedure = await repository.GetForUpdateOrThrowAsync(request.ProcedureId, cancellationToken);
        var stage = procedure.GetStageOrThrow(request.StageId);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new ProcedureChange(procedure, request.Actor, now);

        if (ProcedureWorkflow.WhyStageCantBeEdited(procedure, stage) is { } reason)
            throw new ConflictException(reason);

        change.UpdateStageNotes(stage, request.Notes);

        if (change.HasChanges)
            await repository.SaveAsync(change.AuditEntries, cancellationToken);

        return await ProcedureMapper.ToResponseAsync(procedure, identityReadService, now, cancellationToken);
    }
}
