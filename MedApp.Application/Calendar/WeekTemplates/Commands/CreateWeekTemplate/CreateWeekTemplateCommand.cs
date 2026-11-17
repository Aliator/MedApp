using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.WeekTemplates.Commands.CreateWeekTemplate;

/// <summary>
/// Adds a week template to a member of staff's own templates: one of their day templates (or none) for each weekday,
/// in force from today. Their own, or anyone's for an admin.
/// </summary>
public sealed record CreateWeekTemplateCommand(
    Guid OwnerUserId,
    string Name,
    Guid? Monday,
    Guid? Tuesday,
    Guid? Wednesday,
    Guid? Thursday,
    Guid? Friday,
    CalendarActor Actor)
    : IRequest<WeekTemplateResponse>, IWeekTemplateCommand;
