using MedApp.Contracts.Tasks.PatientTaskStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTaskStages.Templates.Queries.GetPatientTaskStageTemplateById;

public sealed record GetPatientTaskTemplateByIdQuery(Guid TemplateId) : IRequest<PatientTaskStageTemplateResponse>;