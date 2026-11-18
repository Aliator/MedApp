using MedApp.Infrastructure.Common.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Authentication;

/// <summary>MedApp's own columns on Identity's roles table, and each role's permissions; Identity configures the rest.</summary>
public sealed class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.Property(role => role.Description)
            .HasMaxLength(ApplicationRole.DescriptionMaxLength)
            .IsRequired();

        // Goes with the role; nothing else points at a role's permissions.
        builder.HasMany(role => role.Permissions)
            .WithOne()
            .HasForeignKey(permission => permission.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
