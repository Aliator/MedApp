using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.Domain.Procedures.PatientProcedures;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.UpdatePatientProcedure;

public sealed class UpdatePatientProcedureHandler(
    IPatientProcedureRepository repository,
    IIdentityReadService identityReadService,
    IProcedureMonitor procedureMonitor,
    TimeProvider timeProvider)
    : IRequestHandler<UpdatePatientProcedureCommand, PatientProcedureResponse>
{
    public async Task<PatientProcedureResponse> Handle(UpdatePatientProcedureCommand request, CancellationToken cancellationToken)
    {
        var procedure = await repository.GetForUpdateOrThrowAsync(request.Id, cancellationToken);

        if (!ProcedureWorkflow.CanEditDetails(procedure))
            throw new ConflictException($"A procedure that's {ProcedureWorkflow.Describe(procedure.Status)} can't be edited.");

        // What changed, from and to, for the audit log.
        var changes = ProcedureDetails.Apply(
            procedure,
            request.Title?.Trim(),
            request.DueDate,
            request.Priority is null ? null : ProcedureEnums.Parse<PatientProcedurePriority>(request.Priority));

        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (changes.Count > 0)
        {
            var change = new ProcedureChange(procedure, request.Actor, now);
            ProcedureDetails.Record(change, changes);
            await repository.SaveAsync(change.AuditEntries, cancellationToken);
            await procedureMonitor.NotifyAsync(change, cancellationToken);
        }

        return await ProcedureMapper.ToResponseAsync(procedure, identityReadService, now, cancellationToken, viewerId: request.Actor.UserId);
    }
}
