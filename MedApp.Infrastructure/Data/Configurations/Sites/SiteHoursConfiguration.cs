using MedApp.Domain.Sites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Sites;

public sealed class SiteHoursConfiguration : IEntityTypeConfiguration<SiteHours>
{
    public void Configure(EntityTypeBuilder<SiteHours> builder)
    {
        builder.ToTable("SiteHours");

        builder.HasKey(x => x.Id);

        // .NET's DayOfWeek number (Sunday 0 to Saturday 6).
        builder.Property(x => x.DayOfWeek);

        // A site's own detail: deleting an unused site (one nobody's hours name) deletes its opening hours with it.
        builder.HasOne<Site>()
            .WithMany()
            .HasForeignKey(x => x.SiteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.SiteId, x.ValidFrom });
    }
}
