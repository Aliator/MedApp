using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.WeekTemplates.Queries.GetWeekTemplateById;

public sealed record GetWeekTemplateByIdQuery(Guid WeekTemplateId) : IRequest<WeekTemplateResponse>;
