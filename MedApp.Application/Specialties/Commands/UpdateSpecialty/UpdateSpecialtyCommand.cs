using MedApp.Contracts.Specialties.Responses;
using MediatR;

namespace MedApp.Application.Specialties.Commands.UpdateSpecialty;

/// <summary>
/// Renames a specialty and changes its waiting-time targets (each left out stays as it is), from the version the
/// caller saw (<paramref name="ExpectedVersion"/>; 409 if it has changed since). Templates, pathways and staff that
/// have it show the new name; new targets apply only to clocks that copy them afterwards.
/// </summary>
public sealed record UpdateSpecialtyCommand(
    Guid SpecialtyId,
    string Name,
    int ExpectedVersion,
    int? TargetDaysUrgent = null,
    int? TargetDaysHigh = null,
    int? TargetDaysMedium = null,
    int? TargetDaysLow = null)
    : IRequest<SpecialtyResponse>;
