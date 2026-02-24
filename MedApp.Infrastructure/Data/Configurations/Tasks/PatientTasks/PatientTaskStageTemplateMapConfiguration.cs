using MedApp.Domain.Tasks.PatientTask;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations;

public sealed class PatientTaskStageTemplateMapConfiguration : IEntityTypeConfiguration<PatientTaskStageTemplateMap>
{
    public void Configure(EntityTypeBuilder<PatientTaskStageTemplateMap> builder)
    {
        builder.ToTable("PatientTaskStageTemplateMaps");

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