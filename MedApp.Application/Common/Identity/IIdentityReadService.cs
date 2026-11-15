using MedApp.Application.Authentication.Users.Queries.SearchUsers;
using MedApp.Application.Common.Paging;
using MedApp.Contracts.Authentication.Responses;

namespace MedApp.Application.Common.Identity;

public interface IIdentityReadService
{
    Task<IReadOnlyList<string>> GetUsernamesAsync(CancellationToken ct);
    Task<IReadOnlyList<string>> GetRoleNamesAsync(CancellationToken ct);
    Task<IReadOnlyList<string>> GetRolesForUserAsync(
        string username,
        CancellationToken ct);
    Task<UserResponse?> GetUserAsync(
        string username,
        CancellationToken ct);

    Task<IReadOnlyList<UserResponse>> GetUsersAsync(CancellationToken ct);

    /// <summary>The usernames of the users with these IDs. IDs with no account (e.g. deleted) are left out.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetUsernamesByIdAsync(IEnumerable<Guid> userIds, CancellationToken ct);

    /// <summary>One page of users matching the query's filters, sorted by username, and the total that match.</summary>
    Task<PagedResult<UserResponse>> SearchUsersAsync(SearchUsersQuery query, CancellationToken ct);
}