using MedApp.Domain.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Authentication;

public sealed class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
{
    public void Configure(EntityTypeBuilder<UserSettings> builder)
    {
        builder.ToTable("UserSettings");

        // One row per user. No foreign key to the Identity users table, as with UserSessions.
        builder.HasKey(settings => settings.UserId);

        builder.Property(settings => settings.Json)
            .IsRequired();

        builder.Property(settings => settings.UpdatedAtUtc)
            .IsRequired();
    }
}
