using MedApp.Domain.Contacts;
using MedApp.Domain.Practices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Practices;

public sealed class PracticeConfiguration : IEntityTypeConfiguration<Practice>
{
    public void Configure(EntityTypeBuilder<Practice> builder)
    {
        builder.ToTable("Practices");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(Practice.MaxNameLength);

        // Upper case letters and digits; unique when present (a filtered index leaves out practices without one).
        builder.Property(x => x.OdsCode)
            .HasMaxLength(Practice.MaxOdsCodeLength)
            .IsUnicode(false);

        builder.HasIndex(x => x.OdsCode)
            .IsUnique()
            .HasFilter("[OdsCode] IS NOT NULL");

        builder.Property(x => x.AddressLines)
            .HasMaxLength(PostalAddress.MaxStoredLength);

        builder.Property(x => x.Postcode)
            .HasMaxLength(UkPostcodes.MaxLength)
            .IsUnicode(false);

        builder.Property(x => x.Phone)
            .HasMaxLength(UkPhoneNumbers.MaxPhoneLength)
            .IsUnicode(false);

        builder.Property(x => x.SecureEmail)
            .HasMaxLength(Practice.MaxSecureEmailLength);

        builder.Property(x => x.IsArchived)
            .HasDefaultValue(false);

        // Read back with each change: a save from a copy read before another is refused (409).
        builder.Property(x => x.Version)
            .IsRequired()
            .IsConcurrencyToken();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.LastUpdated).IsRequired();

        // The directory's list: by name.
        builder.HasIndex(x => x.Name);
    }
}
