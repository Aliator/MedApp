using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.WeekTemplates.Queries.GetWeekTemplateById;

/// <summary>Anyone's week template: any member of staff can read them.</summary>
public sealed record GetWeekTemplateByIdQuery(Guid WeekTemplateId, CalendarActor Actor) : IRequest<WeekTemplateResponse>;
