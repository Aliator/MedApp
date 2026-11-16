using MedApp.Domain.Calendar;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Calendar;

public sealed class CalendarActivityTypeConfiguration : IEntityTypeConfiguration<CalendarActivityType>
{
    /// <summary>The activity types every database starts with (fixed IDs, so the seed is stable).</summary>
    public static readonly IReadOnlyList<CalendarActivityType> Seeded =
    [
        new() { Id = Guid.Parse("22222222-0000-0000-0000-000000000001"), Name = "Lunch", Colour = "green" },
        new() { Id = Guid.Parse("22222222-0000-0000-0000-000000000002"), Name = "Admin time", Colour = "blue" },
        new() { Id = Guid.Parse("22222222-0000-0000-0000-000000000003"), Name = "Training", Colour = "purple" },
        new() { Id = Guid.Parse("22222222-0000-0000-0000-000000000004"), Name = "Meeting", Colour = "teal" },
        new() { Id = Guid.Parse("22222222-0000-0000-0000-000000000005"), Name = "Annual leave", Colour = "orange" },
        new() { Id = Guid.Parse("22222222-0000-0000-0000-000000000006"), Name = "Travel", Colour = "gray" },
    ];

    public void Configure(EntityTypeBuilder<CalendarActivityType> builder)
    {
        builder.ToTable("CalendarActivityTypes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(60);

        builder.Property(x => x.Colour)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.IsArchived)
            .HasDefaultValue(false);

        builder.HasIndex(x => x.Name)
            .IsUnique();

        builder.HasData(Seeded.Select(t => new CalendarActivityType
        {
            Id = t.Id,
            Name = t.Name,
            Colour = t.Colour,
            IsArchived = false
        }));
    }
}
