using MedApp.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Teams;

public sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    /// <summary>When General was seeded (fixed, so the seed is stable).</summary>
    public static readonly DateTime GeneralCreatedAt = new(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("Teams");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(Team.MaxNameLength);

        builder.HasIndex(x => x.Name)
            .IsUnique();

        builder.Property(x => x.IsArchived)
            .HasDefaultValue(false);

        // Read back with each change: a save from a copy read before another is refused (409).
        builder.Property(x => x.Version)
            .IsRequired()
            .IsConcurrencyToken();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.LastUpdated).IsRequired();

        builder.HasData(new Team
        {
            Id = Team.GeneralId,
            Name = Team.GeneralName,
            IsArchived = false,
            Version = 1,
            CreatedAt = GeneralCreatedAt,
            LastUpdated = GeneralCreatedAt
        });
    }
}
