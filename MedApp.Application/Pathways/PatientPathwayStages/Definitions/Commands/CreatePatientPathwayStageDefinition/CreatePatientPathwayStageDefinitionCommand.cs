using MedApp.Contracts.Pathways.PatientPathwayStages.Definitions.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Definitions.Commands.CreatePatientPathwayStageDefinition;

public sealed record CreatePatientPathwayStageDefinitionCommand(
    string Name,
    string Description,
    string Instructions,
    string? SnomedCode = null,
    string? SnomedTerm = null,
    string VisitRequirement = "None",
    string? NeedsLetterKind = null,
    bool WaitsForResults = false)
    : IRequest<PatientPathwayStageDefinitionResponse>;
