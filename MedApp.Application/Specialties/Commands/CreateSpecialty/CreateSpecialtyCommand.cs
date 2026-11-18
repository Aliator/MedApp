using MedApp.Contracts.Specialties.Responses;
using MediatR;

namespace MedApp.Application.Specialties.Commands.CreateSpecialty;

/// <summary>Adds a specialty: a name, unique ignoring case.</summary>
public sealed record CreateSpecialtyCommand(string Name) : IRequest<SpecialtyResponse>;
