using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.Domain.Procedures.PatientProcedures;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.AssignPatientProcedure;

public sealed class AssignPatientProcedureHandler(
    IPatientProcedureRepository repository,
    IIdentityReadService identityReadService)
    : IRequestHandler<AssignPatientProcedureCommand, List<PatientProcedureAssignmentResponse>>
{
    public async Task<List<PatientProcedureAssignmentResponse>> Handle(
        AssignPatientProcedureCommand request,
        CancellationToken ct)
    {
        var procedure = await repository.GetByIdAsync(request.PatientProcedureId, ct)
                   ?? throw new NotFoundException($"Patient procedure '{request.PatientProcedureId}' was not found.");

        var users = await identityReadService.GetUsersAsync(ct);
        if (users.All(u => u.UserId != request.UserId))
            throw new NotFoundException($"User '{request.UserId}' was not found.");

        if (procedure.Assignments.Any(x => x.UserId == request.UserId))
            throw new ConflictException("This user is already assigned to the patient procedure.");

        var now = DateTime.UtcNow;
       
        procedure.Assignments.Add(new PatientProcedureAssignment
        {
            PatientProcedureId = procedure.Id,
            UserId = request.UserId,
            AssignedByUserId = request.AssignedByUserId,
            AssignedAtUtc = now
            });
        
        if (procedure.Status == PatientProcedureStatus.Unassigned)
        {
            procedure.Status = PatientProcedureStatus.Assigned;
        }
        
        procedure.LastUpdated = now;
       
        await repository.UpdateAsync(procedure, ct);

        return procedure.Assignments
            .Select(x => new PatientProcedureAssignmentResponse
            {
                PatientProcedureId = x.PatientProcedureId,
                UserId = x.UserId,
                AssignedByUserId = x.AssignedByUserId,
                AssignedAtUtc = x.AssignedAtUtc
            })
            .ToList();
    }
}