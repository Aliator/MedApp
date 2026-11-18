using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Terminology;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Pathways.PatientPathways;

public sealed class PatientPathwayStageConfiguration : IEntityTypeConfiguration<PatientPathwayStage>
{
    public void Configure(EntityTypeBuilder<PatientPathwayStage> builder)
    {
        builder.ToTable("PatientPathwayStages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StageOrder)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.LastUpdated)
            .IsRequired();

        builder.Property(x => x.CompletedAtUtc);

        // Copied from the stage definition when the pathway is created (same limits as the definition).
        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100)
            .HasDefaultValue(string.Empty);

        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(500)
            .HasDefaultValue(string.Empty);

        builder.Property(x => x.Instructions)
            .IsRequired()
            .HasMaxLength(2000)
            .HasDefaultValue(string.Empty);

        // A SNOMED CT concept ID (6 to 18 digits) and its display term, both optional (SnomedCt).
        builder.Property(x => x.SnomedCode)
            .HasMaxLength(SnomedCt.MaxLength)
            .IsUnicode(false);

        builder.Property(x => x.SnomedTerm)
            .HasMaxLength(SnomedCt.MaxTermLength);

        builder.Property(x => x.Notes)
            .IsRequired()
            .HasMaxLength(4000)
            .HasDefaultValue(string.Empty);

        builder.Property(x => x.IsLocked)
            .HasDefaultValue(false);

        // Copied from the stage definition when the pathway is created.
        builder.Property(x => x.VisitRequirement)
            .HasDefaultValue(VisitRequirement.None);

        // Copied from the template when the pathway is created: optional stages and branches.
        builder.Property(x => x.IsOptional)
            .HasDefaultValue(false);

        builder.Property(x => x.ChoosesBranch)
            .HasDefaultValue(false);

        builder.Property(x => x.Branch)
            .HasMaxLength(PathwayBranchLimits.MaxBranchLength);

        // Skipped: the reason by number (StageSkipReason), and its detail (free text).
        builder.Property(x => x.SkipDetail)
            .HasMaxLength(PathwayBranchLimits.MaxSkipDetailLength);

        builder.Ignore(x => x.IsSkipped);

        // Copied from the template when the pathway is created.
        builder.Property(x => x.StopsClock)
            .HasDefaultValue(false);

        builder.HasOne(x => x.PatientPathway)
            .WithMany(x => x.Stages)
            .HasForeignKey(x => x.PatientPathwayId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.StageDefinition)
            .WithMany()
            .HasForeignKey(x => x.StageDefinitionId)
            .OnDelete(DeleteBehavior.NoAction);

        // Unique by position, not by definition: a pathway can have the same stage more than once.
        builder.HasIndex(x => new { x.PatientPathwayId, x.StageOrder })
            .IsUnique();
    }
}