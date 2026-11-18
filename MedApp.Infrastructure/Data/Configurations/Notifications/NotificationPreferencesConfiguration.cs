using MedApp.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Notifications;

public sealed class NotificationPreferencesConfiguration : IEntityTypeConfiguration<NotificationPreferences>
{
    public void Configure(EntityTypeBuilder<NotificationPreferences> builder)
    {
        builder.ToTable("NotificationPreferences");

        // One row per user who has chosen; no row means the defaults. No foreign key, as with UserSettings.
        builder.HasKey(preferences => preferences.UserId);

        builder.Property(preferences => preferences.PathwayAssigned)
            .IsRequired();

        builder.Property(preferences => preferences.UpdatedAtUtc)
            .IsRequired();
    }
}
