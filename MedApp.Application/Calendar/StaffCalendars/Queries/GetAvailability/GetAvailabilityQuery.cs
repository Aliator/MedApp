using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Queries.GetAvailability;

/// <summary>
/// When members of staff are all free from one UK date to another (at most 31 days), for arranging a meeting of
/// <paramref name="DurationMinutes"/>: the free time each working day, suggested times, and each person's busy times.
/// </summary>
public sealed record GetAvailabilityQuery(
    IReadOnlyList<Guid> StaffUserIds,
    DateOnly From,
    DateOnly To,
    int DurationMinutes,
    Guid? ExceptMeetingId)
    : IRequest<AvailabilityResponse>;
