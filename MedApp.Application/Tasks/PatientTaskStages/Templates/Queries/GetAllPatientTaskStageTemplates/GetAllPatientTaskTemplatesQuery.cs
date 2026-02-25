using MedApp.Contracts.Tasks.PatientTaskStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTaskStages.Templates.Queries.GetAllPatientTaskStageTemplates;

public sealed record GetAllPatientTaskTemplatesQuery : IRequest<IReadOnlyList<PatientTaskStageTemplateResponse>>;