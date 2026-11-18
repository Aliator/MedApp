using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Definitions.Commands.DeletePatientPathwayStageDefinition;

/// <summary>Deletes a stage definition that no template or pathway has used; one that has been used is archived instead.</summary>
public sealed record DeletePatientPathwayStageDefinitionCommand(Guid StageDefinitionId) : IRequest;
