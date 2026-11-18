using MedApp.Application.Practices.Common;
using MedApp.Application.Practices.Repositories;
using MedApp.Contracts.Practices.Responses;
using MediatR;

namespace MedApp.Application.Practices.Commands.SetPracticeArchived;

public sealed class SetPracticeArchivedHandler(IPracticeRepository repository, TimeProvider timeProvider)
    : IRequestHandler<SetPracticeArchivedCommand, PracticeResponse>
{
    public async Task<PracticeResponse> Handle(SetPracticeArchivedCommand request, CancellationToken cancellationToken)
    {
        var practice = await repository.GetOrThrowAsync(request.PracticeId, cancellationToken);
        PracticeRules.EnsureVersion(practice, request.ExpectedVersion);

        if (practice.IsArchived != request.IsArchived)
        {
            var originalVersion = practice.Version;
            practice.IsArchived = request.IsArchived;
            practice.Version++;
            practice.LastUpdated = timeProvider.GetUtcNow().UtcDateTime;
            await repository.UpdateAsync(practice, originalVersion, cancellationToken);
        }

        return await PracticeMapper.ToResponseAsync(practice, repository, cancellationToken);
    }
}
