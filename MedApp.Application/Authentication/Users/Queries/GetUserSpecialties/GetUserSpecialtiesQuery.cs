using MedApp.Contracts.Specialties.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Users.Queries.GetUserSpecialties;

/// <summary>The specialties a member of staff belongs to, sorted by name; 404 for an unknown username.</summary>
public sealed record GetUserSpecialtiesQuery(string Username) : IRequest<IReadOnlyList<StaffSpecialtyResponse>>;
