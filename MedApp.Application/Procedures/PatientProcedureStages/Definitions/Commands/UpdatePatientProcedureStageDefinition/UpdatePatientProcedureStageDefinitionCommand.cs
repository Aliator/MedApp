using MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.UpdatePatientProcedureStageDefinition;

/// <summary>Edits a stage definition; null fields are left as they are. Procedures already created keep their copy.</summary>
public sealed record UpdatePatientProcedureStageDefinitionCommand(
    Guid StageDefinitionId,
    string? Name,
    string? Description,
    string? Instructions,
    // The code and term change together: null leaves both, an empty code removes both.
    string? SnomedCode = null,
    string? SnomedTerm = null,
    string? VisitRequirement = null)
    : IRequest<PatientProcedureStageDefinitionResponse>;
