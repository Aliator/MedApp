using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.AssignPatientProcedure;

public sealed record AssignPatientProcedureCommand(
    Guid PatientProcedureId,
    Guid UserId,
    Guid AssignedByUserId)
    : IRequest<List<PatientProcedureAssignmentResponse>>;