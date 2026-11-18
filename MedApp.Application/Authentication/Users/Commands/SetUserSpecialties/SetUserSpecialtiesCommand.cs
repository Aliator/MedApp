using MedApp.Contracts.Specialties.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Users.Commands.SetUserSpecialties;

/// <summary>
/// Sets the specialties a member of staff belongs to, exactly (an empty list removes them all). Returns them as they
/// are now.
/// </summary>
public sealed record SetUserSpecialtiesCommand(string Username, IReadOnlyList<Guid> SpecialtyIds)
    : IRequest<IReadOnlyList<StaffSpecialtyResponse>>;
