using MedApp.Contracts.Specialties.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Users.Queries.GetMySpecialties;

/// <summary>The specialties the signed-in user belongs to, sorted by name.</summary>
public sealed record GetMySpecialtiesQuery(Guid UserId) : IRequest<IReadOnlyList<StaffSpecialtyResponse>>;
