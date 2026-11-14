using MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Definitions.Queries.GetPatientProcedureStageDefinitionById;

public sealed record GetPatientProcedureStageDefinitionByIdQuery(Guid StageDefinitionId)
    : IRequest<PatientProcedureStageDefinitionResponse>;
