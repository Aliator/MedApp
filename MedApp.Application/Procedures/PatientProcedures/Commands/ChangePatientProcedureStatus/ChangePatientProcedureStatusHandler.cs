using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.Domain.Procedures.PatientProcedures;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.ChangePatientProcedureStatus;

public sealed class ChangePatientProcedureStatusHandler(
    IPatientProcedureRepository repository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<ChangePatientProcedureStatusCommand, PatientProcedureResponse>
{
    public async Task<PatientProcedureResponse> Handle(ChangePatientProcedureStatusCommand request, CancellationToken cancellationToken)
    {
        var procedure = await repository.GetForUpdateOrThrowAsync(request.Id, cancellationToken);
        var to = ProcedureEnums.Parse<PatientProcedureStatus>(request.Status);

        if (!ProcedureWorkflow.AllowedStatusChanges(procedure).Contains(to))
            throw new ConflictException(
                $"A procedure that's {ProcedureWorkflow.Describe(procedure.Status)} can't be made {ProcedureWorkflow.Describe(to)}.");

        if (to == PatientProcedureStatus.Completed && !ProcedureWorkflow.AllStagesFinished(procedure))
            throw new ConflictException("Every stage must be finished before the procedure is completed.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new ProcedureChange(procedure, request.Actor, now);
        change.ChangeStatus(to, string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim());

        await repository.SaveAsync(change.AuditEntries, cancellationToken);

        return await ProcedureMapper.ToResponseAsync(procedure, identityReadService, now, cancellationToken);
    }
}
