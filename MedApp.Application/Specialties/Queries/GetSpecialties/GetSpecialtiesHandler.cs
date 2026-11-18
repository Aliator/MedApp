using MedApp.Application.Specialties.Common;
using MedApp.Application.Specialties.Repositories;
using MedApp.Contracts.Specialties.Responses;
using MediatR;

namespace MedApp.Application.Specialties.Queries.GetSpecialties;

public sealed class GetSpecialtiesHandler(ISpecialtyRepository repository)
    : IRequestHandler<GetSpecialtiesQuery, IReadOnlyList<SpecialtyResponse>>
{
    public async Task<IReadOnlyList<SpecialtyResponse>> Handle(GetSpecialtiesQuery request, CancellationToken cancellationToken)
    {
        var specialties = await repository.GetAllAsync(request.IncludeArchived, cancellationToken);
        var usage = await repository.GetUsageAsync(specialties.Select(s => s.Id), cancellationToken);

        return specialties
            .Select(s => SpecialtyMapper.ToResponse(s, usage.GetValueOrDefault(s.Id, SpecialtyUsage.None)))
            .ToList();
    }
}
