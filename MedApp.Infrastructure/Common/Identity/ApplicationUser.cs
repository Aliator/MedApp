using Microsoft.AspNetCore.Identity;

namespace MedApp.Infrastructure.Common.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Whether the user must choose their own password before doing anything else: set when an admin's action gives
    /// them a generated one (a new account, or a reset), cleared when they change it. The API refuses everything
    /// else while it's set (<c>PasswordChangeRequired</c>).
    /// </summary>
    public bool MustChangePassword { get; set; }
}
