using MedApp.Application.Common.Identity;
using MediatR;

namespace MedApp.Application.Auth.Users.Queries.GetAllUsers;

public sealed class GetAllUsersHandler(
    IIdentityReadService identityReadService)
    : IRequestHandler<GetAllUsersQuery, IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> Handle(
        GetAllUsersQuery request,
        CancellationToken ct)
    {
        return await identityReadService.GetUsernamesAsync(ct);
    }
}