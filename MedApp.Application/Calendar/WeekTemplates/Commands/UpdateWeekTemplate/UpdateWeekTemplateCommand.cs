using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.WeekTemplates.Commands.UpdateWeekTemplate;

/// <summary>
/// Renames a week template and replaces its days from today: weekdays that didn't change are kept as they are, so past
/// days keep what they had (see <see cref="TemplateHistory"/>).
/// </summary>
public sealed record UpdateWeekTemplateCommand(
    Guid WeekTemplateId,
    string Name,
    Guid? Monday,
    Guid? Tuesday,
    Guid? Wednesday,
    Guid? Thursday,
    Guid? Friday)
    : IRequest<WeekTemplateResponse>, IWeekTemplateCommand;
