using MedApp.Application.Common.Exceptions;
using MedApp.Application.Specialties.Common;
using MedApp.Application.Specialties.Repositories;
using MediatR;

namespace MedApp.Application.Specialties.Commands.DeleteSpecialty;

public sealed class DeleteSpecialtyHandler(ISpecialtyRepository repository) : IRequestHandler<DeleteSpecialtyCommand>
{
    public async Task Handle(DeleteSpecialtyCommand request, CancellationToken cancellationToken)
    {
        var specialty = await repository.GetOrThrowAsync(request.SpecialtyId, cancellationToken);

        var usage = (await repository.GetUsageAsync([specialty.Id], cancellationToken))
            .GetValueOrDefault(specialty.Id, SpecialtyUsage.None);
        if (usage.IsInUse)
            throw new ConflictException(SpecialtyRules.InUseMessage(specialty, usage));

        await repository.DeleteAsync(specialty, cancellationToken);
    }
}
