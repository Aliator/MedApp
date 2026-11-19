using MedApp.Domain.Lab;
using MedApp.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Lab;

/// <summary>The lab's test catalogue (MC13): setup, no patient data. A test a case used is archived, never deleted.</summary>
public sealed class LabTestConfiguration : IEntityTypeConfiguration<LabTest>
{
    public void Configure(EntityTypeBuilder<LabTest> builder)
    {
        builder.ToTable("LabTests");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(LabTest.MaxNameLength);
        builder.HasIndex(x => x.Name).IsUnique();

        builder.Property(x => x.SnomedCode)
            .HasMaxLength(LabTest.MaxSnomedCodeLength)
            .IsUnicode(false);

        // Its steps go with it (a test with cases is never deleted: the handler checks, the cases' key refuses).
        builder.HasMany(x => x.Steps)
            .WithOne()
            .HasForeignKey(s => s.LabTestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class LabTestStepConfiguration : IEntityTypeConfiguration<LabTestStep>
{
    public void Configure(EntityTypeBuilder<LabTestStep> builder)
    {
        builder.ToTable("LabTestSteps");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(LabTestStep.MaxNameLength);

        builder.HasIndex(x => new { x.LabTestId, x.Position });
    }
}

/// <summary>
/// Lab cases (MC15): patient data (the results and the details typed are free text). A patient or pathway with cases
/// can't be deleted (the handlers check; the keys refuse a race).
/// </summary>
public sealed class LabCaseConfiguration : IEntityTypeConfiguration<LabCase>
{
    public void Configure(EntityTypeBuilder<LabCase> builder)
    {
        builder.ToTable("LabCases");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Number)
            .IsRequired()
            .HasMaxLength(Domain.Specimens.Specimen.MaxNumberLength)
            .IsUnicode(false);
        builder.HasIndex(x => x.Number).IsUnique();

        builder.Property(x => x.ReportText).HasMaxLength(LabCase.MaxReportTextLength);
        builder.Property(x => x.RepeatDetail).HasMaxLength(LabCase.MaxDetailLength);
        builder.Property(x => x.ErrorDetail).HasMaxLength(LabCase.MaxDetailLength);

        builder.Property(x => x.Version).IsRequired();

        // Two lab members changing one case at once: the later save is refused (409) instead of overwriting the earlier.
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.LabTest)
            .WithMany()
            .HasForeignKey(x => x.LabTestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Pathway)
            .WithMany()
            .HasForeignKey(x => x.PathwayId)
            .OnDelete(DeleteBehavior.Restrict);

        // The lab stage it belongs to (MC16), read with the pathway's stages.
        builder.HasOne<Domain.Pathways.PatientPathways.PatientPathwayStage>()
            .WithMany(s => s.LabCases)
            .HasForeignKey(x => x.StageId)
            .OnDelete(DeleteBehavior.NoAction);

        // The lab's team, and the clinic that asked (the team walls): archived, never deleted.
        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.RequestingTeam)
            .WithMany()
            .HasForeignKey(x => x.RequestingTeamId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(x => x.Steps)
            .WithOne()
            .HasForeignKey(s => s.LabCaseId)
            .OnDelete(DeleteBehavior.Cascade);

        // The lab's lists: a status's cases by when they were opened; a stage's open case; a patient's, a pathway's.
        builder.HasIndex(x => new { x.Status, x.OpenedAtUtc });
        builder.HasIndex(x => x.StageId);
        builder.HasIndex(x => x.PathwayId);
        builder.HasIndex(x => x.PatientId);
        builder.HasIndex(x => x.RequestingTeamId);
        builder.HasIndex(x => x.HospitalRequestId).HasFilter("[HospitalRequestId] IS NOT NULL");
    }
}

public sealed class LabCaseStepConfiguration : IEntityTypeConfiguration<LabCaseStep>
{
    public void Configure(EntityTypeBuilder<LabCaseStep> builder)
    {
        builder.ToTable("LabCaseSteps");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Detail).HasMaxLength(LabCase.MaxDetailLength);
        builder.Property(x => x.ErrorDetail).HasMaxLength(LabCase.MaxDetailLength);
        builder.Ignore(x => x.IsEnteredInError);

        builder.HasIndex(x => new { x.LabCaseId, x.Sequence }).IsUnique();
    }
}
