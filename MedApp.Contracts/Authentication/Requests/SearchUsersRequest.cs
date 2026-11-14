namespace MedApp.Contracts.Authentication.Requests;

/// <summary>Query string for <c>GET api/auth/users/search</c>. Every filter is optional.</summary>
public sealed record SearchUsersRequest
{
    /// <summary>Page number, from 1.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Rows per page, 1 to 100.</summary>
    public int PageSize { get; init; } = 10;

    /// <summary><c>username</c>, the only sort (and the default).</summary>
    public string? Sort { get; init; }

    public bool Ascending { get; init; } = true;

    /// <summary>Matches usernames containing this text, ignoring case.</summary>
    public string? Username { get; init; }

    /// <summary>Only users with this role, e.g. <c>Admin</c>.</summary>
    public string? Role { get; init; }
}
