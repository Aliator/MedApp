using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.DayTemplates.Queries.GetDayTemplateById;

public sealed record GetDayTemplateByIdQuery(Guid DayTemplateId) : IRequest<DayTemplateResponse>;
