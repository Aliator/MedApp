using MedApp.Infrastructure.Common.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Authentication;

/// <summary>MedApp's own columns on Identity's users table; Identity configures the rest.</summary>
public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        // As long as Identity's own Email column.
        builder.Property(user => user.PendingEmail)
            .HasMaxLength(256);
    }
}
