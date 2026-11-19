using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Infrastructure.Data.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Pathways.PatientPathways;

public sealed class PathwayBranchAdditionConfiguration : IEntityTypeConfiguration<PathwayBranchAddition>
{
    public void Configure(EntityTypeBuilder<PathwayBranchAddition> builder)
    {
        builder.ToTable("PathwayBranchAdditions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(PathwayBranchLimits.MaxBranchLength);

        // Free text (the clinician's reason): exported, and replaced on anonymisation.
        builder.Property(x => x.Reason)
            .IsRequired()
            .HasMaxLength(PathwayBranchLimits.MaxBranchReasonLength);

        builder.Property(x => x.StageIds)
            .HasJsonConversion();

        builder.HasIndex(x => x.PatientPathwayId);
        builder.HasIndex(x => x.PatientId);
    }
}
