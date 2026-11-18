using MedApp.Domain.Letters;
using MedApp.Domain.Patients;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Referrals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Letters;

public sealed class LetterConfiguration : IEntityTypeConfiguration<Letter>
{
    public void Configure(EntityTypeBuilder<Letter> builder)
    {
        builder.ToTable("Letters");

        builder.HasKey(x => x.Id);

        // A patient, pathway or referral with letters can't be deleted (the handlers refuse it first, with a 409).
        builder.HasOne<Patient>()
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PatientPathway>()
            .WithMany()
            .HasForeignKey(x => x.PathwayId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Referral>()
            .WithMany()
            .HasForeignKey(x => x.ReferralId)
            .OnDelete(DeleteBehavior.Restrict);

        // A template a letter was written from is never deleted (only archived), so a foreign key keeps that true.
        builder.HasOne<LetterTemplate>()
            .WithMany()
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.TemplateName)
            .IsRequired()
            .HasMaxLength(LetterTemplate.MaxNameLength);

        // Stored by number (LetterKind and LetterStatus are never renumbered).
        builder.Property(x => x.Kind).IsRequired();
        builder.Property(x => x.Status).IsRequired();

        builder.Property(x => x.Letterhead)
            .IsRequired()
            .HasMaxLength(Letter.MaxLetterheadLength);

        // nvarchar(max): 20,000 characters is over nvarchar's 4,000 limit; the validators enforce it.
        builder.Property(x => x.Body).IsRequired();

        builder.Property(x => x.Closing)
            .IsRequired()
            .HasMaxLength(Letter.MaxClosingLength);

        builder.Property(x => x.ReturnComment).HasMaxLength(Letter.MaxCommentLength);
        builder.Property(x => x.WithdrawnReason).HasMaxLength(Letter.MaxCommentLength);

        builder.Property(x => x.Version).IsRequired();

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.LastUpdated).IsRequired();

        builder.HasMany(x => x.Recipients)
            .WithOne()
            .HasForeignKey(r => r.LetterId)
            .OnDelete(DeleteBehavior.Cascade);

        // The views: a status with its author or approver, oldest first; and a patient's, pathway's or referral's letters.
        builder.HasIndex(x => new { x.Status, x.AuthorUserId });
        builder.HasIndex(x => new { x.Status, x.ApproverUserId });
        builder.HasIndex(x => x.PatientId);
        builder.HasIndex(x => x.PathwayId);
        builder.HasIndex(x => x.ReferralId);
        builder.HasIndex(x => x.TemplateId);
    }
}

public sealed class LetterRecipientConfiguration : IEntityTypeConfiguration<LetterRecipient>
{
    public void Configure(EntityTypeBuilder<LetterRecipient> builder)
    {
        builder.ToTable("LetterRecipients");

        builder.HasKey(x => x.Id);

        // No foreign key to the practice: its name and address are copied here, so what was sent stays as it was, and
        // the directory keeps its own rule for deleting a practice (unused by patients and referrals).
        builder.Property(x => x.Kind).IsRequired();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(LetterRecipient.MaxNameLength);

        builder.Property(x => x.AddressLines).HasMaxLength(LetterRecipient.MaxAddressLength);
        builder.Property(x => x.Postcode).HasMaxLength(LetterRecipient.MaxPostcodeLength);
        builder.Property(x => x.Email).HasMaxLength(LetterRecipient.MaxEmailLength);

        // Stored by number (never renumbered).
        builder.Property(x => x.DispatchStatus).IsRequired();

        builder.Property(x => x.TransportReference)
            .HasMaxLength(LetterRecipient.MaxTransportReferenceLength)
            .IsUnicode(false);

        // The To send view: approved letters with a recipient not yet sent.
        builder.HasIndex(x => new { x.LetterId, x.DispatchStatus });
    }
}
