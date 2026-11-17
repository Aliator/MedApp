using MedApp.Domain.Notepads;
using MedApp.Infrastructure.Common.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Notepads;

public sealed class NotepadConfiguration : IEntityTypeConfiguration<Notepad>
{
    public void Configure(EntityTypeBuilder<Notepad> builder)
    {
        builder.ToTable("Notepads");

        builder.HasKey(x => x.UserId);
        builder.Property(x => x.UserId).ValueGeneratedNever();

        builder.Property(x => x.Text)
            .IsRequired()
            .HasMaxLength(Notepad.MaxTextLength);

        builder.Property(x => x.LastUpdatedUtc).IsRequired();

        builder.Property(x => x.Version).IsRequired();

        builder.Property(x => x.RowVersion).IsRowVersion();

        // One each, deleted with the account.
        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<Notepad>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
