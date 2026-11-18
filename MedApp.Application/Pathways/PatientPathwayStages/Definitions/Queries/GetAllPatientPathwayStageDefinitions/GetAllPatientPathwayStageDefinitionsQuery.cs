using MedApp.Contracts.Pathways.PatientPathwayStages.Definitions.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Definitions.Queries.GetAllPatientPathwayStageDefinitions;

/// <summary>Every stage definition, sorted by name; archived ones only when asked for.</summary>
public sealed record GetAllPatientPathwayStageDefinitionsQuery(bool IncludeArchived = false)
    : IRequest<IReadOnlyList<PatientPathwayStageDefinitionResponse>>;
