using MedApp.Application.Calendar.Common;
using MediatR;

namespace MedApp.Application.Calendar.DayTemplates.Commands.DeleteDayTemplate;

/// <summary>
/// Deletes a day template no week template or day override has ever used; one that has been used is archived instead.
/// Its owner's, or anyone's for an admin.
/// </summary>
public sealed record DeleteDayTemplateCommand(Guid DayTemplateId, CalendarActor Actor) : IRequest;
