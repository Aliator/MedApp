using MedApp.Domain.Specimens;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Specimens;

public sealed class SpecimenTypeConfiguration : IEntityTypeConfiguration<SpecimenType>
{
    public void Configure(EntityTypeBuilder<SpecimenType> builder)
    {
        builder.ToTable("SpecimenTypes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(SpecimenType.MaxNameLength);

        builder.HasIndex(x => x.Name).IsUnique();

        builder.Property(x => x.SnomedCode)
            .HasMaxLength(SpecimenType.MaxSnomedCodeLength)
            .IsUnicode(false);
    }
}
