using MedApp.Domain.Clinics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Clinics;

public sealed class ClinicPatternConfiguration : IEntityTypeConfiguration<ClinicPattern>
{
    public void Configure(EntityTypeBuilder<ClinicPattern> builder)
    {
        builder.ToTable("ClinicPatterns");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasOne(x => x.Room)
            .WithMany()
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Team)
            .WithMany()
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.StaffUserId);
        builder.HasIndex(x => x.RoomId);
    }
}
