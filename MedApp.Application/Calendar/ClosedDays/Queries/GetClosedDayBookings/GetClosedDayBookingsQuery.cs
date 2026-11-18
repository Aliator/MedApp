using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.ClosedDays.Queries.GetClosedDayBookings;

/// <summary>What's booked on a UK date, for an admin about to close it: counts, by member of staff, never patients.</summary>
public sealed record GetClosedDayBookingsQuery(DateOnly Date) : IRequest<DayBookingsResponse>;
