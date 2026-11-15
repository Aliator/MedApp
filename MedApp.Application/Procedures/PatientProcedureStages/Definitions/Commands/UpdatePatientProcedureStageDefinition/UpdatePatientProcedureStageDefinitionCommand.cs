using MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.UpdatePatientProcedureStageDefinition;

/// <summary>Edits a stage definition; null fields are left as they are. Procedures already created keep their copy.</summary>
public sealed record UpdatePatientProcedureStageDefinitionCommand(
    Guid StageDefinitionId,
    string? Name,
    string? Description,
    string? Instructions)
    : IRequest<PatientProcedureStageDefinitionResponse>;
