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
    string? VisitRequirement = null,
    // A letter kind's name, "None" to remove it, or null to leave it as it is.
    string? NeedsLetterKind = null,
    // Whether it waits for results, or null to leave it as it is.
    bool? WaitsForResults = null)
    : IRequest<PatientPathwayStageDefinitionResponse>;
