using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.DayTemplates.Queries.GetDayTemplates;

/// <summary>
/// One member of staff's day templates, sorted by name, with their blocks now; archived ones only when asked for. Any
/// member of staff can read anyone's.
/// </summary>
public sealed record GetDayTemplatesQuery(Guid OwnerUserId, CalendarActor Actor, bool IncludeArchived = false)
    : IRequest<IReadOnlyList<DayTemplateResponse>>;
