using MedApp.Contracts.Specialties.Responses;
using MediatR;

namespace MedApp.Application.Specialties.Commands.CreateSpecialty;

/// <summary>
/// Adds a specialty: a name, unique ignoring case, and its waiting-time targets in days by priority (each left out
/// takes the default: 14 Urgent, 28 High, 126 Medium and Low).
/// </summary>
public sealed record CreateSpecialtyCommand(
    string Name,
    int? TargetDaysUrgent = null,
    int? TargetDaysHigh = null,
    int? TargetDaysMedium = null,
    int? TargetDaysLow = null)
    : IRequest<SpecialtyResponse>;
