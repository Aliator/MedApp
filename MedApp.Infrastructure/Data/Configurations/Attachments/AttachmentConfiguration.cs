using MedApp.Application.Attachments.Common;
using MedApp.Domain.Attachments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Attachments;

public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachments");

        builder.HasKey(x => x.Id);

        // No foreign keys, as with the audit log: a file's record must outlive its patient, pathway, stage or referral until
        // its bytes are purged, or nothing would know they're there. Deleting a patient or pathway with live files
        // is refused (409) instead.
        builder.Property(x => x.FileName)
            .IsRequired()
            .HasMaxLength(AttachmentLimits.MaxFileNameLength);

        builder.Property(x => x.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Sha256)
            .IsRequired()
            .HasMaxLength(64)
            .IsFixedLength()
            .IsUnicode(false);

        builder.Property(x => x.UploadedByUsername)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.DeletedByUsername)
            .HasMaxLength(256);

        builder.Ignore(x => x.IsDeleted);

        // A patient's files, newest first; a pathway's, a stage's and a referral's; and the deleted ones, for the purge.
        builder.HasIndex(x => new { x.PatientId, x.UploadedAtUtc });
        builder.HasIndex(x => x.PathwayId);
        builder.HasIndex(x => x.StageId);
        builder.HasIndex(x => x.ReferralId);
        builder.HasIndex(x => x.DeletedAtUtc);
    }
}
