using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.DayTemplates.Commands.SetDayTemplateArchived;

/// <summary>
/// Archives a day template (it can't be chosen again, but keeps applying where it already does) or restores it. Its
/// owner's, or anyone's for an admin.
/// </summary>
public sealed record SetDayTemplateArchivedCommand(Guid DayTemplateId, bool IsArchived, CalendarActor Actor)
    : IRequest<DayTemplateResponse>;
