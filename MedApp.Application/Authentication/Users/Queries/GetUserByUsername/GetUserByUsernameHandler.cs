using MedApp.Application.Common.Identity;
using MediatR;

namespace MedApp.Application.Authentication.Users.Queries.GetUserByUsername;

public sealed class GetUserByUsernameHandler(
    IIdentityReadService identityReadService)
    : IRequestHandler<GetUserByUsernameQuery, UserDetails?>
{
    public async Task<UserDetails?> Handle(
        GetUserByUsernameQuery request,
        CancellationToken ct)
    {
        return await identityReadService.GetUserAsync(request.Username, ct);
    }
}