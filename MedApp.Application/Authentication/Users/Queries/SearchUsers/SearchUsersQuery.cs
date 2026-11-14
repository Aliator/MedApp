using MedApp.Contracts.Authentication.Responses;
using MedApp.Contracts.Common;
using MediatR;

namespace MedApp.Application.Authentication.Users.Queries.SearchUsers;

/// <summary>One page of users matching the filters, sorted by username.</summary>
public sealed record SearchUsersQuery(
    int Page,
    int PageSize,
    string? Sort,
    bool Ascending,
    string? Username,
    string? Role)
    : IRequest<PagedResponse<UserResponse>>
{
    /// <summary>The only sort users have.</summary>
    public const string SortByUsername = "username";
}
