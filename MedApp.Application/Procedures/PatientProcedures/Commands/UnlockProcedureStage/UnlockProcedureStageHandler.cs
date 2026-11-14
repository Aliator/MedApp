using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.UnlockProcedureStage;

public sealed class UnlockProcedureStageHandler(
    IPatientProcedureRepository repository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<UnlockProcedureStageCommand, PatientProcedureResponse>
{
    public async Task<PatientProcedureResponse> Handle(UnlockProcedureStageCommand request, CancellationToken cancellationToken)
    {
        var procedure = await repository.GetForUpdateOrThrowAsync(request.ProcedureId, cancellationToken);
        var stage = procedure.GetStageOrThrow(request.StageId);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new ProcedureChange(procedure, request.Actor, now);

        if (!ProcedureWorkflow.CanUnlockStage(procedure, stage))
            throw new ConflictException(stage is { IsCompleted: true, IsLocked: true }
                ? $"Stages can't be unlocked while the procedure is {ProcedureWorkflow.Describe(procedure.Status)}."
                : $"The '{stage.Name}' stage isn't finished and locked, so there's nothing to unlock.");

        change.UnlockStage(stage);

        if (change.HasChanges)
            await repository.SaveAsync(change.AuditEntries, cancellationToken);

        return await ProcedureMapper.ToResponseAsync(procedure, identityReadService, now, cancellationToken);
    }
}
