using MedApp.Application.Authentication.Users.Common;
using MedApp.Application.Common.Identity;
using MedApp.Application.Teams.Repositories;
using MedApp.Application.Common.Paging;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Contracts.Common;
using MediatR;

namespace MedApp.Application.Authentication.Users.Queries.SearchUsers;

public sealed class SearchUsersHandler(IIdentityReadService identityReadService, ITeamRepository teams)
    : IRequestHandler<SearchUsersQuery, PagedResponse<UserResponse>>
{
    public async Task<PagedResponse<UserResponse>> Handle(SearchUsersQuery request, CancellationToken ct)
    {
        var found = await identityReadService.SearchUsersAsync(request, ct);
        var result = found with { Items = await UserAccountTypes.WithAccountTypesAsync(found.Items, teams, ct) };

        return PagedMapper.ToResponse(result, request.Page, request.PageSize);
    }
}
