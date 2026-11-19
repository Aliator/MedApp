using MedApp.Domain.Teams;
using MedApp.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Results;

public sealed class ResultConfiguration : IEntityTypeConfiguration<Result>
{
    public void Configure(EntityTypeBuilder<Result> builder)
    {
        builder.ToTable("Results");

        // Its team (the team walls, decision 200): set when it's made, never edited by hand. A team anything has is
        // archived, never deleted.
        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => x.TeamId);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).IsRequired().HasMaxLength(Result.MaxTitleLength);
        builder.Property(x => x.ActionDetail).HasMaxLength(Result.MaxActionDetailLength);
        builder.Property(x => x.ErrorDetail).HasMaxLength(Result.MaxErrorDetailLength);
        builder.Property(x => x.ReportText).HasMaxLength(MedApp.Domain.Lab.LabCase.MaxReportTextLength);

        // The lab case whose report made it (MC15): a case with a result is never deleted.
        builder.HasOne(x => x.LabCase)
            .WithMany()
            .HasForeignKey(x => x.LabCaseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.LabCaseId).HasFilter("[LabCaseId] IS NOT NULL");

        builder.Property(x => x.Version).IsRequired();

        // Two people changing one result at once: the later save is refused (409) instead of overwriting the earlier.
        builder.Property(x => x.RowVersion).IsRowVersion();

        // A patient, pathway or specimen with results can't be deleted (the handlers check; the keys refuse a race).
        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Pathway)
            .WithMany()
            .HasForeignKey(x => x.PathwayId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Specimen)
            .WithMany()
            .HasForeignKey(x => x.SpecimenId)
            .OnDelete(DeleteBehavior.Restrict);

        // One live result per specimen: a report entered in error makes way for the right one (status 3 is EnteredInError).
        builder.HasIndex(x => x.SpecimenId)
            .IsUnique()
            .HasFilter("[SpecimenId] IS NOT NULL AND [Status] <> 3");

        // The results list: a status's results for a clinician, oldest reported first; and a pathway's, a patient's.
        builder.HasIndex(x => new { x.Status, x.ResponsibleUserId, x.ReportedAtUtc });
        builder.HasIndex(x => x.PathwayId);
        builder.HasIndex(x => x.PatientId);

        // Whether a file is a result's report (it can't be deleted on its own, nor be a second result).
        builder.HasIndex(x => x.ReportAttachmentId);
    }
}
