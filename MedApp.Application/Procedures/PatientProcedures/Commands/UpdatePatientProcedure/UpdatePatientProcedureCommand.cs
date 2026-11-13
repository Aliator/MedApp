using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.UpdatePatientProcedure;

public sealed record UpdatePatientProcedureCommand(
    Guid PatientProcedureId,
    string? Title,
    string? Notes,
    DateTime? DueDateUtc,
    string? Priority,
    string? Status,
    IReadOnlyList<Guid>? StageDefinitionIdsInOrder
) : IRequest<PatientProcedureResponse>;