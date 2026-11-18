using MedApp.Domain.Contacts;
using MedApp.Domain.Patients;
using MedApp.Domain.Practices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Patients;

public sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("Patients");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Email)
            .HasMaxLength(256);

        // Ten digits, no spaces. Optional, and unique when present: a filtered index leaves out the patients without one.
        builder.Property(x => x.NhsNumber)
            .HasMaxLength(10)
            .IsUnicode(false);

        builder.HasIndex(x => x.NhsNumber)
            .IsUnique()
            .HasFilter("[NhsNumber] IS NOT NULL");

        // Contact details, all optional: the address's lines as one value, the postcode normalised, a mobile as +447….
        builder.Property(x => x.AddressLines)
            .HasMaxLength(PostalAddress.MaxStoredLength);

        builder.Property(x => x.Postcode)
            .HasMaxLength(UkPostcodes.MaxLength)
            .IsUnicode(false);

        builder.Property(x => x.MobilePhone)
            .HasMaxLength(UkPhoneNumbers.MaxMobileLength)
            .IsUnicode(false);

        // The GP practice they're registered with: one any patient has is archived, never deleted (the key refuses too).
        builder.HasOne<Practice>()
            .WithMany()
            .HasForeignKey(x => x.PracticeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.DateOfBirth)
            .IsRequired()
            .HasConversion(
                d => d.ToDateTime(TimeOnly.MinValue),
                d => DateOnly.FromDateTime(d)
            )
            .HasColumnType("date");

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.LastUpdated)
            .IsRequired();

        builder.Property(x => x.RestrictionReason)
            .HasMaxLength(500);

        builder.Property(x => x.DisputeReason)
            .HasMaxLength(500);

        builder.Property(x => x.RetentionKeptReason)
            .HasMaxLength(500);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();
    }
}
