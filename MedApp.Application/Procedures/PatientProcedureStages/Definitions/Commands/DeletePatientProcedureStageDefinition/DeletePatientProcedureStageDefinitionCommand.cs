using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.DeletePatientProcedureStageDefinition;

public sealed record DeletePatientProcedureStageDefinitionCommand(Guid StageDefinitionId) : IRequest<bool>;