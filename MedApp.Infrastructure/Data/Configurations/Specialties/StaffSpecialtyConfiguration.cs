using MedApp.Domain.Specialties;
using MedApp.Infrastructure.Common.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Specialties;

public sealed class StaffSpecialtyConfiguration : IEntityTypeConfiguration<StaffSpecialty>
{
    public void Configure(EntityTypeBuilder<StaffSpecialty> builder)
    {
        builder.ToTable("StaffSpecialties");

        builder.HasKey(x => new { x.UserId, x.SpecialtyId });

        // Goes with the account.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // A specialty anyone belongs to is archived, never deleted.
        builder.HasOne(x => x.Specialty)
            .WithMany()
            .HasForeignKey(x => x.SpecialtyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SpecialtyId);
    }
}
