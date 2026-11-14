using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.UnassignPatientProcedure;

public sealed class UnassignPatientProcedureHandler(
    IPatientProcedureRepository repository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<UnassignPatientProcedureCommand, PatientProcedureResponse>
{
    public async Task<PatientProcedureResponse> Handle(UnassignPatientProcedureCommand request, CancellationToken cancellationToken)
    {
        var procedure = await repository.GetForUpdateOrThrowAsync(request.ProcedureId, cancellationToken);

        if (!ProcedureWorkflow.CanChangeAssignments(procedure))
            throw new ConflictException($"Staff can't be unassigned from a procedure that's {ProcedureWorkflow.Describe(procedure.Status)}.");

        var assignment = procedure.Assignments.FirstOrDefault(a => a.UserId == request.UserId)
                         ?? throw new NotFoundException($"User '{request.UserId}' isn't assigned to this procedure.");

        var usernames = await identityReadService.GetUsernamesByIdAsync([request.UserId], cancellationToken);
        var username = usernames.GetValueOrDefault(request.UserId, ProcedureMapper.DeletedUser);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new ProcedureChange(procedure, request.Actor, now);
        change.Unassign(assignment, username);

        await repository.SaveAsync(change.AuditEntries, cancellationToken);

        return await ProcedureMapper.ToResponseAsync(procedure, identityReadService, now, cancellationToken);
    }
}
