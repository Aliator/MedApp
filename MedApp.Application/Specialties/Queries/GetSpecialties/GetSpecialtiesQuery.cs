using MedApp.Contracts.Specialties.Responses;
using MediatR;

namespace MedApp.Application.Specialties.Queries.GetSpecialties;

/// <summary>Every specialty, sorted by name, with what has each; archived ones only when asked for.</summary>
public sealed record GetSpecialtiesQuery(bool IncludeArchived = false) : IRequest<IReadOnlyList<SpecialtyResponse>>;
