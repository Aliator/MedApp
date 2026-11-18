using MedApp.Contracts.Specialties.Responses;
using MediatR;

namespace MedApp.Application.Specialties.Commands.SetSpecialtyArchived;

/// <summary>
/// Archives a specialty (it can't be chosen for templates or given to staff any more; what has it keeps it) or
/// restores it, from the version the caller saw (<paramref name="ExpectedVersion"/>; 409 if it has changed since).
/// </summary>
public sealed record SetSpecialtyArchivedCommand(Guid SpecialtyId, bool IsArchived, int ExpectedVersion)
    : IRequest<SpecialtyResponse>;
