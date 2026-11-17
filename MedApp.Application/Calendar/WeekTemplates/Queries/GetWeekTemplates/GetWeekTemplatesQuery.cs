using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.WeekTemplates.Queries.GetWeekTemplates;

/// <summary>
/// One member of staff's week templates, sorted by name, with their days now; archived ones only when asked for. Any
/// member of staff can read anyone's.
/// </summary>
public sealed record GetWeekTemplatesQuery(Guid OwnerUserId, CalendarActor Actor, bool IncludeArchived = false)
    : IRequest<IReadOnlyList<WeekTemplateResponse>>;
