using MediatR;

namespace MedApp.Application.Calendar.WeekTemplates.Commands.DeleteWeekTemplate;

/// <summary>Deletes a week template that has never been anyone's default; one that has is archived instead.</summary>
public sealed record DeleteWeekTemplateCommand(Guid WeekTemplateId) : IRequest;
