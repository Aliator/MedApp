using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.DayTemplates.Commands.CreateDayTemplate;

/// <summary>Adds a day template with its blocks, in force from today.</summary>
public sealed record CreateDayTemplateCommand(string Name, IReadOnlyList<DayTemplateBlockValue> Blocks)
    : IRequest<DayTemplateResponse>, IDayTemplateCommand;
