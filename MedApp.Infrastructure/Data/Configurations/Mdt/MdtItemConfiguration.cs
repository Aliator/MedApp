using MedApp.Domain.Mdt;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Mdt;

public sealed class MdtItemConfiguration : IEntityTypeConfiguration<MdtItem>
{
    public void Configure(EntityTypeBuilder<MdtItem> builder)
    {
        builder.ToTable("MdtItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Question)
            .IsRequired()
            .HasMaxLength(MdtItem.MaxQuestionLength);

        builder.Property(x => x.Outcome)
            .HasMaxLength(MdtItem.MaxOutcomeLength);

        builder.Property(x => x.ListedByUsername)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.OutcomeByUsername)
            .HasMaxLength(256);

        builder.Property(x => x.WithdrawnByUsername)
            .HasMaxLength(256);

        builder.Property(x => x.Version)
            .IsRequired();

        // Two people changing one item at once: the later save is refused (409) instead of overwriting the earlier.
        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.Ignore(x => x.IsOpen);

        // A patient with pathways can't be deleted, nor a team in use: the keys refuse too, should either race.
        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Team)
            .WithMany()
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        // Deleting the pathway deletes its items in the same save (PatientPathwayRepository.DeleteAsync), and
        // cancelling a meeting clears its items' meeting (MeetingRepository): done by hand, not by the database, so
        // SQL Server never sees two cascade paths.
        builder.HasOne(x => x.Pathway)
            .WithMany()
            .HasForeignKey(x => x.PathwayId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Meeting)
            .WithMany()
            .HasForeignKey(x => x.MeetingId)
            .OnDelete(DeleteBehavior.NoAction);

        // Who was present: the item owns them.
        builder.HasMany(x => x.Attendees)
            .WithOne()
            .HasForeignKey(x => x.MdtItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.MeetingId);
        builder.HasIndex(x => x.PathwayId);
        builder.HasIndex(x => x.PatientId);
        builder.HasIndex(x => new { x.TeamId, x.Status });
    }
}
