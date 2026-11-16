using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.WeekTemplates.Commands.SetWeekTemplateArchived;

/// <summary>
/// Archives a week template (it can't be chosen again, but stays the default of whoever has it) or restores it.
/// </summary>
public sealed record SetWeekTemplateArchivedCommand(Guid WeekTemplateId, bool IsArchived) : IRequest<WeekTemplateResponse>;
