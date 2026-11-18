using MedApp.Domain.Pathways.PatientPathways;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Pathways.PatientPathways;

public sealed class PatientPathwayAssignmentConfiguration : IEntityTypeConfiguration<PatientPathwayAssignment>
{
    public void Configure(EntityTypeBuilder<PatientPathwayAssignment> builder)
    {
        builder.ToTable("PatientPathwayAssignments");
        
        builder.HasKey(x => new { x.PatientPathwayId, x.UserId });

        builder.Property(x => x.AssignedAtUtc)
            .IsRequired();

        builder.Property(x => x.AssignedByUserId)
            .IsRequired();

        builder.HasOne(x => x.PatientPathway)
            .WithMany(x => x.Assignments)
            .HasForeignKey(x => x.PatientPathwayId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasIndex(x => x.AssignedByUserId);
    }
}