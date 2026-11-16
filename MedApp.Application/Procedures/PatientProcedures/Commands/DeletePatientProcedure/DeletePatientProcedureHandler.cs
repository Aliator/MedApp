using MedApp.Application.Common.Exceptions;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.DeletePatientProcedure;

public sealed class DeletePatientProcedureHandler(
    IPatientProcedureRepository repository,
    IProcedureMonitor procedureMonitor,
    TimeProvider timeProvider)
    : IRequestHandler<DeletePatientProcedureCommand>
{
    public async Task Handle(DeletePatientProcedureCommand request, CancellationToken cancellationToken)
    {
        var procedure = await repository.GetForUpdateOrThrowAsync(request.Id, cancellationToken);

        if (!ProcedureWorkflow.CanBeDeleted(procedure))
            throw new ConflictException("Only cancelled or archived procedures can be deleted. Cancel it first.");

        var change = new ProcedureChange(procedure, request.Actor, timeProvider.GetUtcNow().UtcDateTime);
        change.Audit(PatientProcedureAuditAction.Deleted, new { status = procedure.Status.ToString() });

        await repository.DeleteAsync(procedure, change.AuditEntries, cancellationToken);

        // No rule watches for deletions yet; every procedure command still asks, so a new event needs no handler change.
        await procedureMonitor.NotifyAsync(change, cancellationToken);
    }
}
