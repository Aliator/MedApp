using MedApp.Infrastructure.Common.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Authentication;

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");

        builder.HasKey(permission => new { permission.RoleId, permission.Permission });

        // A permission's name: ASCII letters only.
        builder.Property(permission => permission.Permission)
            .HasMaxLength(RolePermission.PermissionMaxLength)
            .IsUnicode(false);
    }
}
