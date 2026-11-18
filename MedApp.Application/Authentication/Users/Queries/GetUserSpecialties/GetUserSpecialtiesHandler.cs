using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Specialties.Common;
using MedApp.Application.Specialties.Repositories;
using MedApp.Contracts.Specialties.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Users.Queries.GetUserSpecialties;

public sealed class GetUserSpecialtiesHandler(IIdentityReadService readService, ISpecialtyRepository repository)
    : IRequestHandler<GetUserSpecialtiesQuery, IReadOnlyList<StaffSpecialtyResponse>>
{
    public async Task<IReadOnlyList<StaffSpecialtyResponse>> Handle(
        GetUserSpecialtiesQuery request,
        CancellationToken cancellationToken)
    {
        var user = await readService.GetUserAsync(request.Username, cancellationToken)
                   ?? throw new NotFoundException($"User '{request.Username}' was not found.");

        return SpecialtyMapper.ToStaffResponses(await repository.GetForUserAsync(user.UserId, cancellationToken));
    }
}
