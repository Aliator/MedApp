using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.DayTemplates.Queries.GetDayTemplateById;

/// <summary>Anyone's day template: any member of staff can read them (the calendar names the template a day came from).</summary>
public sealed record GetDayTemplateByIdQuery(Guid DayTemplateId, CalendarActor Actor) : IRequest<DayTemplateResponse>;
