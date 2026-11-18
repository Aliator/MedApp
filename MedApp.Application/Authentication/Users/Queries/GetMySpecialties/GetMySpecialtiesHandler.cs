using MedApp.Application.Specialties.Common;
using MedApp.Application.Specialties.Repositories;
using MedApp.Contracts.Specialties.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Users.Queries.GetMySpecialties;

public sealed class GetMySpecialtiesHandler(ISpecialtyRepository repository)
    : IRequestHandler<GetMySpecialtiesQuery, IReadOnlyList<StaffSpecialtyResponse>>
{
    public async Task<IReadOnlyList<StaffSpecialtyResponse>> Handle(
        GetMySpecialtiesQuery request,
        CancellationToken cancellationToken) =>
        SpecialtyMapper.ToStaffResponses(await repository.GetForUserAsync(request.UserId, cancellationToken));
}
