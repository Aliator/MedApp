using MedApp.Domain.Procedures.PatientProcedures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Procedures.PatientProcedures;

public sealed class PatientProcedureStageTemplateMapConfiguration : IEntityTypeConfiguration<PatientProcedureStageTemplateMap>
{
    public void Configure(EntityTypeBuilder<PatientProcedureStageTemplateMap> builder)
    {
        builder.ToTable("PatientProcedureStageTemplateMaps");

        // Keyed by position, so a template can list the same stage more than once.
        builder.HasKey(x => new { x.TemplateId, x.StageOrder });

        builder.Property(x => x.StageOrder)
            .IsRequired();

        builder.HasOne(x => x.Template)
            .WithMany(x => x.Maps)
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.StageDefinition)
            .WithMany()
            .HasForeignKey(x => x.StageDefinitionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => x.StageDefinitionId);
    }
}