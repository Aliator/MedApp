using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.WeekTemplates.Queries.GetWeekTemplates;

/// <summary>Every week template, sorted by name, with its days now; archived ones only when asked for.</summary>
public sealed record GetWeekTemplatesQuery(bool IncludeArchived = false) : IRequest<IReadOnlyList<WeekTemplateResponse>>;
