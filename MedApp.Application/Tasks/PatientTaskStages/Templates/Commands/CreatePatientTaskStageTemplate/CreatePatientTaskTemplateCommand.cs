using MedApp.Contracts.Tasks.PatientTaskStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTaskStages.Templates.Commands.CreatePatientTaskStageTemplate;

public sealed record CreatePatientTaskTemplateCommand(
    string Name,
    IReadOnlyList<Guid> StageDefinitionIdsInOrder
) : IRequest<PatientTaskStageTemplateResponse>;