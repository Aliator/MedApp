using MedApp.Application.Common.Identity;
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

        // On letters (signatures, the approver's line): staff personal data, set by the person or an admin.
        builder.Property(user => user.FullName)
            .HasMaxLength(StaffDetailsRules.MaxLength);

        builder.Property(user => user.JobTitle)
            .HasMaxLength(StaffDetailsRules.MaxLength);

        // TwoFactorMethod is stored as its number (0, none, until two-factor is turned on); the effective method is worked
        // out from it and TwoFactorEnabled.
        builder.Ignore(user => user.EffectiveTwoFactorMethod);
    }
}
