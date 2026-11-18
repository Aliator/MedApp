using MedApp.Domain.DataRequests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.DataRequests;

public sealed class DataRequestConfiguration : IEntityTypeConfiguration<DataRequest>
{
    public void Configure(EntityTypeBuilder<DataRequest> builder)
    {
        builder.ToTable("DataRequests");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RequesterName).HasMaxLength(DataRequest.MaxRequesterNameLength);
        builder.Property(x => x.RequesterRelationship).HasMaxLength(DataRequest.MaxRelationshipLength);
        builder.Property(x => x.IdentityCheckedByUsername).HasMaxLength(256);
        builder.Property(x => x.IdentityCheckDetail).HasMaxLength(DataRequest.MaxDetailLength);
        builder.Property(x => x.ExtensionReason).HasMaxLength(DataRequest.MaxDetailLength);
        builder.Property(x => x.RefusalReason).HasMaxLength(DataRequest.MaxDetailLength);
        builder.Property(x => x.ClosedByUsername).HasMaxLength(256);
        builder.Property(x => x.ReviewedByUsername).HasMaxLength(256);
        builder.Property(x => x.CreatedByUsername).IsRequired().HasMaxLength(256);

        builder.Property(x => x.Version).IsRequired();

        // Two people changing one request at once: the later save is refused (409) instead of overwriting the earlier.
        builder.Property(x => x.RowVersion).IsRowVersion();

        // A patient with data requests can't be deleted (DeletePatientHandler); the foreign key refuses a race too.
        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.WithheldItems)
            .WithOne()
            .HasForeignKey(x => x.DataRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.PatientId);

        // The list (open ones by due date) and the daily reminder.
        builder.HasIndex(x => new { x.Status, x.DueOn });
    }
}

public sealed class DataRequestWithheldItemConfiguration : IEntityTypeConfiguration<DataRequestWithheldItem>
{
    public void Configure(EntityTypeBuilder<DataRequestWithheldItem> builder)
    {
        builder.ToTable("DataRequestWithheldItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Reason).IsRequired().HasMaxLength(DataRequest.MaxDetailLength);

        builder.HasIndex(x => new { x.DataRequestId, x.Kind, x.ItemId }).IsUnique();
    }
}
