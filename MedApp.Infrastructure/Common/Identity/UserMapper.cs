using MedApp.Contracts.Authentication.Responses;

namespace MedApp.Infrastructure.Common.Identity;

/// <summary>
/// Identity's users as the API returns them. It lives here, not in Application, because ApplicationUser is an
/// Infrastructure type.
/// </summary>
public static class UserMapper
{
    /// <summary>
    /// The account, its roles, its email address (only a confirmed one; an unconfirmed one is pending) and whether it
    /// uses two-factor authentication.
    /// </summary>
    public static UserResponse ToResponse(ApplicationUser user, IEnumerable<string> roles) =>
        new(
            user.Id,
            user.UserName ?? string.Empty,
            roles.ToList(),
            user.EmailConfirmed && !string.IsNullOrEmpty(user.Email) ? user.Email : null,
            user.PendingEmail,
            user.TwoFactorEnabled);
}
