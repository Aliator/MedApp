using MedApp.Application.Sites.Common;
using MedApp.Domain.Sites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Sites;

public sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    /// <summary>The unique index on a room's name within its site.</summary>
    public const string SiteAndNameIndex = "IX_Rooms_SiteId_Name";

    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("Rooms");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(SiteLimits.MaxRoomNameLength);

        builder.Property(x => x.IsArchived)
            .HasDefaultValue(false);

        // A site with rooms can't be deleted, only archived.
        builder.HasOne<Site>()
            .WithMany()
            .HasForeignKey(x => x.SiteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.SiteId, x.Name }, SiteAndNameIndex)
            .IsUnique();
    }
}
