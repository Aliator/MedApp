using MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.CreatePatientProcedureStageDefinition;

public sealed record CreatePatientProcedureStageDefinitionCommand(
    string Name,
    string Description,
    string Instructions
) : IRequest<PatientProcedureStageDefinitionResponse>;