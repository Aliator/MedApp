using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.DayTemplates.Commands.CreateDayTemplate;

/// <summary>
/// Adds a day template with its blocks to a member of staff's own templates, in force from today. Their own, or
/// anyone's for an admin.
/// </summary>
public sealed record CreateDayTemplateCommand(
    Guid OwnerUserId,
    string Name,
    IReadOnlyList<DayTemplateBlockValue> Blocks,
    CalendarActor Actor)
    : IRequest<DayTemplateResponse>, IDayTemplateCommand;
