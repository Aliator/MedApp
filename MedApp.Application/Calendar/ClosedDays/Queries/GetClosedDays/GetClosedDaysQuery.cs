using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.ClosedDays.Queries.GetClosedDays;

/// <summary>The closed days from <paramref name="From"/> to <paramref name="To"/> (either may be left open), by date.</summary>
public sealed record GetClosedDaysQuery(DateOnly? From, DateOnly? To) : IRequest<IReadOnlyList<ClosedDayResponse>>;
