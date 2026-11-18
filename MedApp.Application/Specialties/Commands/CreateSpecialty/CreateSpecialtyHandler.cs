using MedApp.Application.Common.Exceptions;
using MedApp.Application.Specialties.Common;
using MedApp.Application.Specialties.Repositories;
using MedApp.Contracts.Specialties.Responses;
using MedApp.Domain.Specialties;
using MediatR;

namespace MedApp.Application.Specialties.Commands.CreateSpecialty;

public sealed class CreateSpecialtyHandler(ISpecialtyRepository repository, TimeProvider timeProvider)
    : IRequestHandler<CreateSpecialtyCommand, SpecialtyResponse>
{
    public async Task<SpecialtyResponse> Handle(CreateSpecialtyCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await repository.NameExistsAsync(name, null, cancellationToken))
            throw new ConflictException(SpecialtyRules.NameTakenMessage(name));

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var specialty = new Specialty
        {
            Id = Guid.NewGuid(),
            Name = name,
            Version = 1,
            CreatedAt = now,
            LastUpdated = now
        };

        await repository.AddAsync(specialty, cancellationToken);

        return SpecialtyMapper.ToResponse(specialty, SpecialtyUsage.None);
    }
}
