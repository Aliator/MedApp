using MedApp.Contracts.Pathways.PatientPathwayStages.Definitions.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Definitions.Commands.UpdatePatientPathwayStageDefinition;

/// <summary>Edits a stage definition; null fields are left as they are. Pathways already created keep their copy.</summary>
public sealed record UpdatePatientPathwayStageDefinitionCommand(
    Guid StageDefinitionId,
    string? Name,
    string? Description,
    string? Instructions,
    // The code and term change together: null leaves both, an empty code removes both.
    string? SnomedCode = null,
    string? SnomedTerm = null,
    string? VisitRequirement = null)
    : IRequest<PatientPathwayStageDefinitionResponse>;
