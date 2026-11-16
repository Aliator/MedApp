using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.WeekTemplates.Commands.CreateWeekTemplate;

/// <summary>Adds a week template: a day template (or none) for each weekday, in force from today.</summary>
public sealed record CreateWeekTemplateCommand(
    string Name,
    Guid? Monday,
    Guid? Tuesday,
    Guid? Wednesday,
    Guid? Thursday,
    Guid? Friday)
    : IRequest<WeekTemplateResponse>, IWeekTemplateCommand;
