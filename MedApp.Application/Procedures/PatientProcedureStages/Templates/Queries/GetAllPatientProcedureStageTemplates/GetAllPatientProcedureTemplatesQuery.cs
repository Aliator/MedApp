using MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Templates.Queries.GetAllPatientProcedureStageTemplates;

/// <summary>Every template with its stages, sorted by name; archived ones only when asked for.</summary>
public sealed record GetAllPatientProcedureTemplatesQuery(bool IncludeArchived = false)
    : IRequest<IReadOnlyList<PatientProcedureStageTemplateResponse>>;
