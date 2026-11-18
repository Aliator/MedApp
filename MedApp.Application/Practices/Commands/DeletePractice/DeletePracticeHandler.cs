using MedApp.Application.Common.Exceptions;
using MedApp.Application.Practices.Common;
using MedApp.Application.Practices.Repositories;
using MediatR;

namespace MedApp.Application.Practices.Commands.DeletePractice;

public sealed class DeletePracticeHandler(IPracticeRepository repository) : IRequestHandler<DeletePracticeCommand>
{
    public async Task Handle(DeletePracticeCommand request, CancellationToken cancellationToken)
    {
        var practice = await repository.GetOrThrowAsync(request.PracticeId, cancellationToken);

        var usage = (await repository.GetUsageAsync([practice.Id], cancellationToken))
            .GetValueOrDefault(practice.Id, PracticeUsage.None);
        if (usage.IsInUse)
            throw new ConflictException(PracticeRules.InUseMessage(practice, usage));

        await repository.DeleteAsync(practice, cancellationToken);
    }
}
