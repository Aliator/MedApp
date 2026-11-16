using MediatR;

namespace MedApp.Application.Calendar.DayTemplates.Commands.DeleteDayTemplate;

/// <summary>
/// Deletes a day template no week template or day override has ever used; one that has been used is archived instead.
/// </summary>
public sealed record DeleteDayTemplateCommand(Guid DayTemplateId) : IRequest;
