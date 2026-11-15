using MedApp.Domain.Procedures.PatientProcedures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Procedures.PatientProcedures;

public sealed class PatientProcedureStageTemplateConfiguration : IEntityTypeConfiguration<PatientProcedureStageTemplate>
{
    public void Configure(EntityTypeBuilder<PatientProcedureStageTemplate> builder)
    {
        builder.ToTable("PatientProcedureStageTemplates");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.HasMany(x => x.Maps)
            .WithOne(x => x.Template)
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.IsArchived)
            .HasDefaultValue(false);

        builder.HasIndex(x => x.Name)
            .IsUnique();
    }
}