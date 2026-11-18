using MediatR;

namespace MedApp.Application.Specialties.Commands.DeleteSpecialty;

/// <summary>Deletes a specialty no template, pathway or member of staff has; one that's in use is archived instead (409).</summary>
public sealed record DeleteSpecialtyCommand(Guid SpecialtyId) : IRequest;
