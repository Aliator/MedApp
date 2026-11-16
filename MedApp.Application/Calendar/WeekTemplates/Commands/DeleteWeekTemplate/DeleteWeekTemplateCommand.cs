using MedApp.Application.Calendar.Common;
using MediatR;

namespace MedApp.Application.Calendar.WeekTemplates.Commands.DeleteWeekTemplate;

/// <summary>
/// Deletes a week template that has never been anyone's default; one that has is archived instead. Its owner's, or
/// anyone's for an admin.
/// </summary>
public sealed record DeleteWeekTemplateCommand(Guid WeekTemplateId, CalendarActor Actor) : IRequest;
