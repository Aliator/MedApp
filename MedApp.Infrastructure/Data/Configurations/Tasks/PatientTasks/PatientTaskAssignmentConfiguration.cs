using MedApp.Domain.Tasks.PatientTask;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations;

public sealed class PatientTaskAssignmentConfiguration : IEntityTypeConfiguration<PatientTaskAssignment>
{
    public void Configure(EntityTypeBuilder<PatientTaskAssignment> builder)
    {
        builder.ToTable("PatientTaskAssignments");

        builder.Property(x => x.AssignedAtUtc)
            .IsRequired();

        builder.Property(x => x.AssignedByUserId)
            .IsRequired();

        builder.HasOne(x => x.PatientTask)
            .WithMany(x => x.Assignments)
            .HasForeignKey(x => x.PatientTaskId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasIndex(x => x.AssignedByUserId);
    }
}