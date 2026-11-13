using MedApp.Domain.Procedures.PatientProcedures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Procedures.PatientProcedures;

public sealed class PatientProcedureStageTemplateMapConfiguration : IEntityTypeConfiguration<PatientProcedureStageTemplateMap>
{
    public void Configure(EntityTypeBuilder<PatientProcedureStageTemplateMap> builder)
    {
        builder.ToTable("PatientProcedureStageTemplateMaps");

        builder.HasKey(x => new { x.TemplateId, x.StageDefinitionId });

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

        builder.HasIndex(x => new { x.TemplateId, x.StageOrder })
            .IsUnique();
    }
}