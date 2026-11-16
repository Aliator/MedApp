using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.DayTemplates.Queries.GetDayTemplates;

/// <summary>Every day template, sorted by name, with its blocks now; archived ones only when asked for.</summary>
public sealed record GetDayTemplatesQuery(bool IncludeArchived = false) : IRequest<IReadOnlyList<DayTemplateResponse>>;
