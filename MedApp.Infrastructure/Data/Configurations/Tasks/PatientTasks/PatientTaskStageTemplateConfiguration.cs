using MedApp.Domain.Tasks.PatientTask;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations;

public sealed class PatientTaskStageTemplateConfiguration : IEntityTypeConfiguration<PatientTaskStageTemplate>
{
    public void Configure(EntityTypeBuilder<PatientTaskStageTemplate> builder)
    {
        builder.ToTable("PatientTaskStageTemplates");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasPrecision(3);

        builder.Property(x => x.LastUpdated)
            .IsRequired()
            .HasPrecision(3);

        builder.HasMany(x => x.Maps)
            .WithOne(x => x.Template)
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Name)
            .IsUnique();
    }
}