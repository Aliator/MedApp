using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Templates.Queries.GetPathwayTemplateById;

public sealed record GetPathwayTemplateByIdQuery(Guid TemplateId) : IRequest<PathwayTemplateResponse>;
