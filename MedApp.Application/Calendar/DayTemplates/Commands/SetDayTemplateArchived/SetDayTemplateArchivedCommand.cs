using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.DayTemplates.Commands.SetDayTemplateArchived;

/// <summary>
/// Archives a day template (it can't be chosen again, but keeps applying where it already does) or restores it.
/// </summary>
public sealed record SetDayTemplateArchivedCommand(Guid DayTemplateId, bool IsArchived) : IRequest<DayTemplateResponse>;
