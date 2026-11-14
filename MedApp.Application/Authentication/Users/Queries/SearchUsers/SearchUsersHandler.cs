using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Paging;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Contracts.Common;
using MediatR;

namespace MedApp.Application.Authentication.Users.Queries.SearchUsers;

public sealed class SearchUsersHandler(IIdentityReadService identityReadService)
    : IRequestHandler<SearchUsersQuery, PagedResponse<UserResponse>>
{
    public async Task<PagedResponse<UserResponse>> Handle(SearchUsersQuery request, CancellationToken ct)
    {
        var result = await identityReadService.SearchUsersAsync(request, ct);

        return new PagedResponse<UserResponse>(
            result.Items,
            request.Page,
            request.PageSize,
            result.TotalCount,
            Paging.TotalPages(result.TotalCount, request.PageSize));
    }
}
