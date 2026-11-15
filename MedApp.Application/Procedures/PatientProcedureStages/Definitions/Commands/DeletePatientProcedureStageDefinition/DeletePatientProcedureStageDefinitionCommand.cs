using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.DeletePatientProcedureStageDefinition;

/// <summary>Deletes a stage definition that no template or procedure has used; one that has been used is archived instead.</summary>
public sealed record DeletePatientProcedureStageDefinitionCommand(Guid StageDefinitionId) : IRequest;
