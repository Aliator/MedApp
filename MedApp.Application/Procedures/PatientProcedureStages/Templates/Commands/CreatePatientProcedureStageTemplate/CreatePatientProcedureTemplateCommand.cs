using MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.CreatePatientProcedureStageTemplate;

/// <summary>Creates a template: an ordered list of stages, in which a stage may appear more than once.</summary>
public sealed record CreatePatientProcedureTemplateCommand(
    string Name,
    IReadOnlyList<Guid> StageDefinitionIdsInOrder)
    : IRequest<PatientProcedureStageTemplateResponse>;
