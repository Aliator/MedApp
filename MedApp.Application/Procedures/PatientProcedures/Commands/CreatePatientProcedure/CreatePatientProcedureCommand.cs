using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.CreatePatientProcedure;

public sealed record CreatePatientProcedureCommand(
    Guid PatientId,
    string Title,
    string Notes,
    DateTime DueDateUtc,
    string Priority,
    IReadOnlyList<Guid> StageDefinitionIdsInOrder
) : IRequest<PatientProcedureResponse>;