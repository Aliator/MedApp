using MedApp.Application.Sites.Common;
using MedApp.Domain.Sites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Sites;

public sealed class SiteConfiguration : IEntityTypeConfiguration<Site>
{
    /// <summary>The unique index on a site's name.</summary>
    public const string NameIndex = "IX_Sites_Name";

    /// <summary>When the main site was added: a fixed time, so the seed is stable.</summary>
    public static readonly DateTime MainSiteCreatedAt = new(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<Site> builder)
    {
        builder.ToTable("Sites");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(SiteLimits.MaxNameLength);

        builder.Property(x => x.AddressLines)
            .HasMaxLength(SiteLimits.MaxAddressLength);

        builder.Property(x => x.Postcode)
            .HasMaxLength(UkPostcodes.MaxLength)
            .IsUnicode(false);

        builder.Property(x => x.Phone)
            .HasMaxLength(SiteLimits.MaxPhoneLength)
            .IsUnicode(false);

        builder.Property(x => x.IsArchived)
            .HasDefaultValue(false);

        builder.HasIndex(x => x.Name, NameIndex)
            .IsUnique();

        // Every database starts with one site, which admins rename (decision 118).
        builder.HasData(new Site
        {
            Id = Site.MainSiteId,
            Name = Site.MainSiteName,
            IsArchived = false,
            CreatedAt = MainSiteCreatedAt,
            LastUpdated = MainSiteCreatedAt
        });
    }
}
