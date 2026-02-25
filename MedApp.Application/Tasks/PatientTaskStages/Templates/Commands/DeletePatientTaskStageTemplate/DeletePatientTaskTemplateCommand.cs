using MediatR;

namespace MedApp.Application.Tasks.PatientTaskStages.Templates.Commands.DeletePatientTaskStageTemplate;

public sealed record DeletePatientTaskTemplateCommand(Guid TemplateId) : IRequest<bool>;