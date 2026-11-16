using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Patients.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.Domain.Procedures.PatientProcedures;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.ChangePatientProcedureStatus;

public sealed class ChangePatientProcedureStatusHandler(
    IPatientProcedureRepository repository,
    IIdentityReadService identityReadService,
    IProcedureMonitor procedureMonitor,
    TimeProvider timeProvider)
    : IRequestHandler<ChangePatientProcedureStatusCommand, PatientProcedureResponse>
{
    public async Task<PatientProcedureResponse> Handle(ChangePatientProcedureStatusCommand request, CancellationToken cancellationToken)
    {
        var to = ProcedureEnums.Parse<PatientProcedureStatus>(request.Status);
        // Blocking and cancelling stay allowed while the patient's record is restricted, so care can be paused safely.
        var procedure = PatientRules.PausesWhileRestricted.Contains(to)
            ? await repository.GetForPauseOrThrowAsync(request.Id, cancellationToken)
            : await repository.GetForUpdateOrThrowAsync(request.Id, cancellationToken);

        if (!ProcedureWorkflow.AllowedStatusChanges(procedure).Contains(to))
            throw new ConflictException(
                $"A procedure that's {ProcedureWorkflow.Describe(procedure.Status)} can't be made {ProcedureWorkflow.Describe(to)}.");

        if (to == PatientProcedureStatus.Completed && !ProcedureWorkflow.AllStagesFinished(procedure))
            throw new ConflictException("Every stage must be finished before the procedure is completed.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new ProcedureChange(procedure, request.Actor, now);
        change.ChangeStatus(to, string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim());

        await repository.SaveAsync(change.AuditEntries, cancellationToken);
        await procedureMonitor.NotifyAsync(change, cancellationToken);

        return await ProcedureMapper.ToResponseAsync(procedure, identityReadService, now, cancellationToken);
    }
}
