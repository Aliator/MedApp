using MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Definitions.Queries.GetAllPatientProcedureStageDefinitions;

/// <summary>Every stage definition, sorted by name; archived ones only when asked for.</summary>
public sealed record GetAllPatientProcedureStageDefinitionsQuery(bool IncludeArchived = false)
    : IRequest<IReadOnlyList<PatientProcedureStageDefinitionResponse>>;
