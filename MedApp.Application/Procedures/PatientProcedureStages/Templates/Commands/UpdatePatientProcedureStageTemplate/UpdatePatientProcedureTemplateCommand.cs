using MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.UpdatePatientProcedureStageTemplate;

/// <summary>Renames a template and/or replaces its stage list. Procedures already created from it keep their stages.</summary>
public sealed record UpdatePatientProcedureTemplateCommand(
    Guid TemplateId,
    string? Name,
    IReadOnlyList<Guid>? StageDefinitionIdsInOrder)
    : IRequest<PatientProcedureStageTemplateResponse>;
