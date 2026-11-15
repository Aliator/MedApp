using MedApp.Domain.Procedures.PatientProcedures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Procedures.PatientProcedures;

public sealed class PatientProcedureAssignmentConfiguration : IEntityTypeConfiguration<PatientProcedureAssignment>
{
    public void Configure(EntityTypeBuilder<PatientProcedureAssignment> builder)
    {
        builder.ToTable("PatientProcedureAssignments");
        
        builder.HasKey(x => new { x.PatientProcedureId, x.UserId });

        builder.Property(x => x.AssignedAtUtc)
            .IsRequired();

        builder.Property(x => x.AssignedByUserId)
            .IsRequired();

        builder.HasOne(x => x.PatientProcedure)
            .WithMany(x => x.Assignments)
            .HasForeignKey(x => x.PatientProcedureId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasIndex(x => x.AssignedByUserId);
    }
}