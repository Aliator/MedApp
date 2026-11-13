using MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.UpdatePatientProcedureStageDefinition;

public sealed record UpdatePatientProcedureStageDefinitionCommand(
    Guid StageDefinitionId,
    string? Name,
    string? Description,
    string? Instructions
) : IRequest<PatientProcedureStageDefinitionResponse>;