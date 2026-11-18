using MedApp.Application.Common.Exceptions;
using MedApp.Application.Specialties.Common;
using MedApp.Application.Specialties.Repositories;
using MedApp.Contracts.Specialties.Responses;
using MediatR;

namespace MedApp.Application.Specialties.Commands.UpdateSpecialty;

public sealed class UpdateSpecialtyHandler(ISpecialtyRepository repository, TimeProvider timeProvider)
    : IRequestHandler<UpdateSpecialtyCommand, SpecialtyResponse>
{
    public async Task<SpecialtyResponse> Handle(UpdateSpecialtyCommand request, CancellationToken cancellationToken)
    {
        var specialty = await repository.GetOrThrowAsync(request.SpecialtyId, cancellationToken);
        SpecialtyRules.EnsureVersion(specialty, request.ExpectedVersion);

        var name = request.Name.Trim();
        if (name == specialty.Name)
            return await SpecialtyMapper.ToResponseAsync(specialty, repository, cancellationToken);

        if (await repository.NameExistsAsync(name, specialty.Id, cancellationToken))
            throw new ConflictException(SpecialtyRules.NameTakenMessage(name));

        var originalVersion = specialty.Version;
        specialty.Name = name;
        specialty.Version++;
        specialty.LastUpdated = timeProvider.GetUtcNow().UtcDateTime;
        await repository.UpdateAsync(specialty, originalVersion, cancellationToken);

        return await SpecialtyMapper.ToResponseAsync(specialty, repository, cancellationToken);
    }
}
