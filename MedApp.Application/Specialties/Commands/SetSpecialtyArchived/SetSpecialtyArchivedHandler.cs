using MedApp.Application.Common.Exceptions;
using MedApp.Application.Specialties.Common;
using MedApp.Application.Specialties.Repositories;
using MedApp.Contracts.Specialties.Responses;
using MediatR;

namespace MedApp.Application.Specialties.Commands.SetSpecialtyArchived;

public sealed class SetSpecialtyArchivedHandler(ISpecialtyRepository repository, TimeProvider timeProvider)
    : IRequestHandler<SetSpecialtyArchivedCommand, SpecialtyResponse>
{
    public async Task<SpecialtyResponse> Handle(SetSpecialtyArchivedCommand request, CancellationToken cancellationToken)
    {
        var specialty = await repository.GetOrThrowAsync(request.SpecialtyId, cancellationToken);
        SpecialtyRules.EnsureVersion(specialty, request.ExpectedVersion);

        if (request.IsArchived && !specialty.IsArchived)
        {
            var usage = (await repository.GetUsageAsync([specialty.Id], cancellationToken)).GetValueOrDefault(specialty.Id);
            if (usage is { AwaitingTriageCount: > 0 })
                throw new ConflictException(SpecialtyRules.AwaitingTriageMessage(specialty, usage.AwaitingTriageCount));
        }

        if (specialty.IsArchived != request.IsArchived)
        {
            var originalVersion = specialty.Version;
            specialty.IsArchived = request.IsArchived;
            specialty.Version++;
            specialty.LastUpdated = timeProvider.GetUtcNow().UtcDateTime;
            await repository.UpdateAsync(specialty, originalVersion, cancellationToken);
        }

        return await SpecialtyMapper.ToResponseAsync(specialty, repository, cancellationToken);
    }
}
