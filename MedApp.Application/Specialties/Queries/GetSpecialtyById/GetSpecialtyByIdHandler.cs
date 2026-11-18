using MedApp.Application.Specialties.Common;
using MedApp.Application.Specialties.Repositories;
using MedApp.Contracts.Specialties.Responses;
using MediatR;

namespace MedApp.Application.Specialties.Queries.GetSpecialtyById;

public sealed class GetSpecialtyByIdHandler(ISpecialtyRepository repository)
    : IRequestHandler<GetSpecialtyByIdQuery, SpecialtyResponse>
{
    public async Task<SpecialtyResponse> Handle(GetSpecialtyByIdQuery request, CancellationToken cancellationToken)
    {
        var specialty = await repository.GetOrThrowAsync(request.SpecialtyId, cancellationToken);
        return await SpecialtyMapper.ToResponseAsync(specialty, repository, cancellationToken);
    }
}
