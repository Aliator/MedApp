using MedApp.Contracts.Authentication.Responses;

namespace MedApp.Infrastructure.Common.Identity;

/// <summary>
/// Identity's users as the API returns them. It lives here, not in Application, because ApplicationUser is an
/// Infrastructure type.
/// </summary>
public static class UserMapper
{
    public static UserResponse ToResponse(ApplicationUser user, IEnumerable<string> roles) =>
        new(user.Id, user.UserName ?? string.Empty, roles.ToList());
}
