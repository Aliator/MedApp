using MedApp.Contracts.Appointments.Responses;
using MedApp.Contracts.Common;
using MediatR;

namespace MedApp.Application.Appointments.Queries.SearchAppointments;

/// <summary>
/// One page of appointments. Every filter is optional; <paramref name="Statuses"/> is any of several, and
/// <paramref name="From"/> and <paramref name="To"/> are UK dates the start falls within (inclusive).
/// </summary>
public sealed record SearchAppointmentsQuery(
    int Page,
    int PageSize,
    string? Sort,
    bool Ascending,
    Guid? PatientId,
    Guid? PathwayId,
    Guid? StageId,
    Guid? StaffUserId,
    IReadOnlyList<string>? Statuses,
    DateOnly? From,
    DateOnly? To,
    string? Search)
    : IRequest<PagedResponse<AppointmentResponse>>;
