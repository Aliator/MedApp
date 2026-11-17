using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.WeekTemplates.Commands.SetWeekTemplateArchived;

/// <summary>
/// Archives a week template (it can't be chosen again, but stays the default of whoever has it) or restores it. Its
/// owner's, or anyone's for an admin.
/// </summary>
public sealed record SetWeekTemplateArchivedCommand(Guid WeekTemplateId, bool IsArchived, CalendarActor Actor)
    : IRequest<WeekTemplateResponse>;
