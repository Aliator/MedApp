using MedApp.Domain.Pathways.PatientPathways;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Pathways.PatientPathways;

public sealed class PathwayNoteDeletionRequestConfiguration : IEntityTypeConfiguration<PathwayNoteDeletionRequest>
{
    /// <summary>The unique index that allows one pending request per note.</summary>
    public const string PendingPerNoteIndex = "IX_PathwayNoteDeletionRequests_NoteId_Pending";

    public void Configure(EntityTypeBuilder<PathwayNoteDeletionRequest> builder)
    {
        builder.ToTable("PathwayNoteDeletionRequests");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        // No foreign keys, like the audit log: a request outlives its note's pathway, and is found by its patient.
        builder.Property(x => x.RequestedByUsername)
            .IsRequired()
            .HasMaxLength(256);

        // A fixed choice, stored as its number; the detail is free text.
        builder.Property(x => x.Reason).IsRequired();
        builder.Property(x => x.Detail).HasMaxLength(PatientPathwayNote.MaxDeletionDetailLength);

        builder.Property(x => x.RequestedAtUtc).IsRequired();
        builder.Property(x => x.Status).IsRequired();

        builder.Property(x => x.DecidedByUsername).HasMaxLength(256);

        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.Ignore(x => x.IsPending);

        // One pending request per note: the handler checks first, and the note's row version stops two at once; this is
        // the last word. A violation is recognised by the index's name (NoteDeletionRequestRepository).
        builder.HasIndex(x => x.NoteId, PendingPerNoteIndex)
            .IsUnique()
            .HasFilter($"[Status] = {(int)NoteDeletionRequestStatus.Pending}");

        // A pathway's waiting requests (closed when it's deleted), and a patient's export and anonymisation.
        builder.HasIndex(x => new { x.PatientPathwayId, x.Status });
        builder.HasIndex(x => x.PatientId);
    }
}
