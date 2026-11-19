using MedApp.Domain.Pathways.PatientPathways;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Pathways.PatientPathways;

public sealed class PatientPathwayStageLinkConfiguration : IEntityTypeConfiguration<PatientPathwayStageLink>
{
    public void Configure(EntityTypeBuilder<PatientPathwayStageLink> builder)
    {
        builder.ToTable("PatientPathwayStageLinks");

        builder.HasKey(x => new { x.FromStageId, x.ToStageId });

        // No foreign keys to the stages (two cascade paths from the pathway): the pathway's cascade removes them.
        builder.HasIndex(x => x.PatientPathwayId);
        builder.HasIndex(x => x.ToStageId);
    }
}
