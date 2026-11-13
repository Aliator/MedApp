using MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.UpdatePatientProcedureStageTemplate;

public sealed record UpdatePatientProcedureTemplateCommand(
    Guid TemplateId,
    string? Name,
    IReadOnlyList<Guid>? StageDefinitionIdsInOrder
) : IRequest<PatientProcedureStageTemplateResponse>;