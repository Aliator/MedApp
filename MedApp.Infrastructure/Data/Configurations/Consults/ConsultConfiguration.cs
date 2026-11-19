using MedApp.Domain.Consults;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Consults;

public sealed class ConsultConfiguration : IEntityTypeConfiguration<Consult>
{
    /// <summary>One open consult (open or replied) per team asked per pathway: a race to send two is refused (a 409).</summary>
    public const string OpenPerTeamIndex = "IX_Consults_PathwayId_AskedTeamId_Open";

    public void Configure(EntityTypeBuilder<Consult> builder)
    {
        builder.ToTable("Consults");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Question)
            .IsRequired()
            .HasMaxLength(Consult.MaxQuestionLength);

        builder.Property(x => x.Reply)
            .HasMaxLength(Consult.MaxReplyLength);

        builder.Property(x => x.RequestedByUsername)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.RepliedByUsername)
            .HasMaxLength(256);

        builder.Property(x => x.ClosedByUsername)
            .HasMaxLength(256);

        builder.Property(x => x.Version)
            .IsRequired();

        // Two people changing one consult at once: the later save is refused (409) instead of overwriting the earlier.
        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.Ignore(x => x.IsOpen);

        // A patient with pathways can't be deleted, nor a team in use: the keys refuse too, should either race.
        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AskingTeam)
            .WithMany()
            .HasForeignKey(x => x.AskingTeamId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.AskedTeam)
            .WithMany()
            .HasForeignKey(x => x.AskedTeamId)
            .OnDelete(DeleteBehavior.NoAction);

        // Deleting the pathway deletes its consults in the same save (PatientPathwayRepository.DeleteAsync), by hand,
        // not by the database, so SQL Server never sees two cascade paths.
        builder.HasOne(x => x.Pathway)
            .WithMany()
            .HasForeignKey(x => x.PathwayId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => x.PathwayId);
        builder.HasIndex(x => x.PatientId);
        // The asked team's incoming list: its open consults.
        builder.HasIndex(x => new { x.AskedTeamId, x.Status });
        builder.HasIndex(x => new { x.PathwayId, x.AskedTeamId })
            .IsUnique()
            .HasFilter("[Status] IN (0, 1)")
            .HasDatabaseName(OpenPerTeamIndex);
    }
}
