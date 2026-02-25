using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Users.Queries.GetUserByUsername;

public sealed class GetUserByUsernameHandler(
    IIdentityReadService identityReadService)
    : IRequestHandler<GetUserByUsernameQuery, UserResponse?>
{
    public async Task<UserResponse?> Handle(
        GetUserByUsernameQuery request,
        CancellationToken ct)
    {
        return await identityReadService.GetUserAsync(request.Username, ct);
    }
}