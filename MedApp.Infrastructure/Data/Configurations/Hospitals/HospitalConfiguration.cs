using MedApp.Domain.Hospitals;
using MedApp.Domain.Lab;
using MedApp.Domain.Patients;
using MedApp.Domain.Specimens;
using MedApp.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Hospitals;

/// <summary>Hospitals (MC18): setup, no patient data. One a request names is archived, never deleted.</summary>
public sealed class HospitalConfiguration : IEntityTypeConfiguration<Hospital>
{
    public void Configure(EntityTypeBuilder<Hospital> builder)
    {
        builder.ToTable("Hospitals");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Name).IsRequired().HasMaxLength(Hospital.MaxNameLength);
        builder.HasIndex(x => x.Name).IsUnique();

        builder.Property(x => x.OdsCode).IsRequired().HasMaxLength(Hospital.MaxOdsCodeLength).IsUnicode(false);
        builder.HasIndex(x => x.OdsCode).IsUnique();

        builder.Property(x => x.Contact).HasMaxLength(Hospital.MaxContactLength);
        builder.Property(x => x.ReportDeliveryMethod).IsRequired();
        builder.Property(x => x.DefaultDeadlineDays).IsRequired();
        builder.Property(x => x.Version).IsRequired();
    }
}

/// <summary>
/// Hospital requests (MC18): patient data (the hospital's patient number, the NHS number as given, the surgeon's name),
/// the lab's own. In <c>PatientRecordRepository</c>, so the export and anonymisation cover them.
/// </summary>
public sealed class HospitalRequestConfiguration : IEntityTypeConfiguration<HospitalRequest>
{
    public void Configure(EntityTypeBuilder<HospitalRequest> builder)
    {
        builder.ToTable("HospitalRequests");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.HospitalPatientNumber)
            .IsRequired()
            .HasMaxLength(HospitalRequest.MaxHospitalPatientNumberLength);
        builder.Property(x => x.NhsNumber).HasMaxLength(NhsNumbers.Length).IsUnicode(false);
        builder.Property(x => x.SurgeonName).IsRequired().HasMaxLength(HospitalRequest.MaxSurgeonNameLength);
        builder.Property(x => x.Priority).IsRequired();
        builder.Property(x => x.LabCaseNumber)
            .IsRequired()
            .HasMaxLength(Specimen.MaxNumberLength)
            .IsUnicode(false);

        builder.HasOne(x => x.Hospital)
            .WithMany()
            .HasForeignKey(x => x.HospitalId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SpecimenType)
            .WithMany()
            .HasForeignKey(x => x.SpecimenTypeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.LabTest)
            .WithMany()
            .HasForeignKey(x => x.LabTestId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.NoAction);

        // The intake list, newest first; a hospital's requests; a patient's.
        builder.HasIndex(x => x.RegisteredAtUtc);
        builder.HasIndex(x => x.HospitalId);
        builder.HasIndex(x => x.PatientId);
        builder.HasIndex(x => x.LabCaseId);
    }
}
