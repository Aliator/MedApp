using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.DayTemplates.Commands.UpdateDayTemplate;

/// <summary>
/// Renames a day template and replaces its blocks from today: blocks that didn't change are kept as they are, so
/// past days keep what they had and exceptions to unchanged blocks stay (see <see cref="TemplateHistory"/>).
/// </summary>
public sealed record UpdateDayTemplateCommand(Guid DayTemplateId, string Name, IReadOnlyList<DayTemplateBlockValue> Blocks)
    : IRequest<DayTemplateResponse>, IDayTemplateCommand;
