using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Users.Queries.GetAllUsers;

public sealed class GetAllUsersHandler(IIdentityReadService identityReadService)
    : IRequestHandler<GetAllUsersQuery, IReadOnlyList<UserResponse>>
{
    public async Task<IReadOnlyList<UserResponse>> Handle(
        GetAllUsersQuery request,
        CancellationToken ct)
    {
        return await identityReadService.GetUsersAsync(ct);
    }
}