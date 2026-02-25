using MedApp.Contracts.Tasks.PatientTaskStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTaskStages.Templates.Commands.UpdatePatientTaskStageTemplate;

public sealed record UpdatePatientTaskTemplateCommand(
    Guid TemplateId,
    string? Name,
    IReadOnlyList<Guid>? StageDefinitionIdsInOrder
) : IRequest<PatientTaskStageTemplateResponse>;