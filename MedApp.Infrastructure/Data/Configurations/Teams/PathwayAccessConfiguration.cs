using MedApp.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Teams;

public sealed class PathwayAccessConfiguration : IEntityTypeConfiguration<PathwayAccess>
{
    public void Configure(EntityTypeBuilder<PathwayAccess> builder)
    {
        builder.ToTable("PathwayAccess");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        // No foreign key to the pathway or the patient: who could see a pathway outlives its deletion (decision 201).
        // A team's grants keep their team: a team with any is archived, never deleted.
        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Property(x => x.Reason).IsRequired();
        builder.Property(x => x.StartedAtUtc).IsRequired();

        // The walls' one EXISTS seeks this: a pathway's grants for the viewer's teams (TeamAccess.Pathways).
        builder.HasIndex(x => new { x.PathwayId, x.TeamId })
            .HasDatabaseName("IX_PathwayAccess_PathwayId_TeamId");
        builder.HasIndex(x => new { x.PathwayId, x.UserId });
        // A team's or a person's live grants, for their lists.
        builder.HasIndex(x => new { x.TeamId, x.EndedAtUtc });
        builder.HasIndex(x => x.PatientId);
        // An MDT item's attendee grants (MC26), to sync and end them.
        builder.HasIndex(x => x.MdtItemId).HasFilter("[MdtItemId] IS NOT NULL");
    }
}
