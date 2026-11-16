using MedApp.Domain.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Authentication;

public sealed class SecuritySettingsConfiguration : IEntityTypeConfiguration<SecuritySettings>
{
    public void Configure(EntityTypeBuilder<SecuritySettings> builder)
    {
        builder.ToTable("SecuritySettings");

        builder.HasKey(settings => settings.Id);

        // The one row's ID is always SecuritySettings.SingletonId, never generated.
        builder.Property(settings => settings.Id)
            .ValueGeneratedNever();
    }
}
