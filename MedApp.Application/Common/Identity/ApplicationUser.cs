using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Common.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public DateTime CreatedAt { get; set; }
}