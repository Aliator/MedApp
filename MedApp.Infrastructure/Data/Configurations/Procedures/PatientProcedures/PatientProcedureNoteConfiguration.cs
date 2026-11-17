using MedApp.Domain.Procedures.PatientProcedures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Procedures.PatientProcedures;

public sealed class PatientProcedureNoteConfiguration : IEntityTypeConfiguration<PatientProcedureNote>
{
    public void Configure(EntityTypeBuilder<PatientProcedureNote> builder)
    {
        builder.ToTable("PatientProcedureNotes");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Text)
            .IsRequired()
            .HasMaxLength(PatientProcedureNote.MaxTextLength);

        builder.Property(x => x.CreatedAtUtc).IsRequired();

        builder.Property(x => x.Version).IsRequired();

        builder.Property(x => x.RowVersion).IsRowVersion();

        // A note goes with its procedure; its versions (no foreign key) and audit entries stay.
        builder.HasOne(x => x.PatientProcedure)
            .WithMany(x => x.Notes)
            .HasForeignKey(x => x.PatientProcedureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.PatientProcedureId, x.CreatedAtUtc });
    }
}
