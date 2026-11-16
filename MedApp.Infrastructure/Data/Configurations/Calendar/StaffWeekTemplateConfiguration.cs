using MedApp.Domain.Calendar;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Calendar;

public sealed class StaffWeekTemplateConfiguration : IEntityTypeConfiguration<StaffWeekTemplate>
{
    public void Configure(EntityTypeBuilder<StaffWeekTemplate> builder)
    {
        builder.ToTable("StaffWeekTemplates");

        builder.HasKey(x => x.Id);

        // No foreign key to the users table for StaffUserId. Week templates in use are archived, never deleted.
        builder.HasOne(x => x.WeekTemplate)
            .WithMany()
            .HasForeignKey(x => x.WeekTemplateId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => new { x.StaffUserId, x.ValidFrom });
    }
}
