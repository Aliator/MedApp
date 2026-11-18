using MedApp.Contracts.Specialties.Responses;
using MediatR;

namespace MedApp.Application.Specialties.Commands.UpdateSpecialty;

/// <summary>
/// Renames a specialty, from the version the caller saw (<paramref name="ExpectedVersion"/>; 409 if it has changed
/// since). Templates, pathways and staff that have it show the new name.
/// </summary>
public sealed record UpdateSpecialtyCommand(Guid SpecialtyId, string Name, int ExpectedVersion) : IRequest<SpecialtyResponse>;
