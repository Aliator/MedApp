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

        // At most one lab that isn't archived (decision 181, MC3): the lab serves every clinic.
        builder.HasIndex(x => x.Kind)
            .IsUnique()
            .HasFilter("[Kind] = 1 AND [IsArchived] = 0")
            .HasDatabaseName("IX_Teams_OneLab");

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
        }, new Team
        {
            Id = Team.LabId,
            Name = Team.LabName,
            IsArchived = false,
            Kind = TeamKind.Lab,
            Version = 1,
            CreatedAt = GeneralCreatedAt,
            LastUpdated = GeneralCreatedAt
        });
    }
}
