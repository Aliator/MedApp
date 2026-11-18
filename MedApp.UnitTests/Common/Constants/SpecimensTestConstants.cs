using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Specimens;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>Synthetic specimens, their types and labs, and the people who handle them.</summary>
public static class SpecimensTestConstants
{
    public const string BiopsyName = "Skin biopsy (punch)";
    public const string UrineName = "Urine";
    public const string InHouseLabName = "Centre laboratory";
    public const string PartnerLabName = "Northgate partner lab";
    public const string Number = "MA26-000123";
    public const int BiopsyDays = 14;

    /// <summary>A clinician who records specimens and can work on pathways (so can be a requesting clinician).</summary>
    public static readonly PathwayActor Nurse = new(
        Staff.UserId, Staff.Username,
        new HashSet<Permission> { Permission.UseApp, Permission.ViewPatients, Permission.WorkPathways, Permission.RecordSpecimens });

    /// <summary>A lab member of staff who receives, rejects and reports specimens.</summary>
    public static readonly PathwayActor LabStaff = new(
        OtherUserId, OtherUsername,
        new HashSet<Permission> { Permission.UseApp, Permission.ViewPatients, Permission.ProcessSpecimens });

    /// <summary>An admin: corrects records, but neither records nor processes specimens (they're clinical).</summary>
    public static readonly PathwayActor Corrector = new(
        Admin.UserId, Admin.Username, Permissions.Admin);

    public static SpecimenType Biopsy() => new()
    {
        Id = Guid.Parse("5bec0000-0000-4000-8000-000000000001"),
        Name = BiopsyName,
        ExpectedTurnaroundDays = BiopsyDays,
        CreatedAt = Now.AddDays(-30),
        LastUpdated = Now.AddDays(-30),
    };

    public static SpecimenType Urine() => new()
    {
        Id = Guid.Parse("5bec0000-0000-4000-8000-000000000002"),
        Name = UrineName,
        ExpectedTurnaroundDays = 2,
        CreatedAt = Now.AddDays(-30),
        LastUpdated = Now.AddDays(-30),
    };

    public static Laboratory InHouseLab() => new()
    {
        Id = Guid.Parse("1ab00000-0000-4000-8000-000000000001"),
        Name = InHouseLabName,
        UsesMedApp = true,
        CreatedAt = Now.AddDays(-30),
        LastUpdated = Now.AddDays(-30),
    };

    public static Laboratory PartnerLab() => new()
    {
        Id = Guid.Parse("1ab00000-0000-4000-8000-000000000002"),
        Name = PartnerLabName,
        CreatedAt = Now.AddDays(-30),
        LastUpdated = Now.AddDays(-30),
    };

    /// <summary>
    /// A specimen taken a day ago at the pathway's first stage by <see cref="Nurse"/> (who asked for it too), in
    /// <paramref name="status"/> with the times and people each step before it would have set, at version 3.
    /// </summary>
    public static Specimen CreateSpecimen(
        PatientPathway? pathway = null,
        SpecimenStatus status = SpecimenStatus.Taken,
        DateTime? takenAtUtc = null,
        DateTime? recordedAtUtc = null)
    {
        pathway ??= CreatePathway(PatientPathwayStatus.InProgress);
        var taken = takenAtUtc ?? Now.AddDays(-1);
        var specimen = new Specimen
        {
            Id = Guid.NewGuid(),
            Number = Number,
            PatientId = pathway.PatientId,
            Patient = pathway.Patient,
            PathwayId = pathway.Id,
            Pathway = pathway,
            StageId = pathway.StageAt(1).Id,
            SpecimenType = Biopsy(),
            Laboratory = InHouseLab(),
            Site = "left forearm",
            RequestNotes = "Synthetic request notes.",
            RequestingUserId = Nurse.UserId,
            Status = status,
            TakenAtUtc = taken,
            TakenByUserId = Nurse.UserId,
            RecordedAtUtc = recordedAtUtc ?? taken,
            Version = 3,
        };
        specimen.SpecimenTypeId = specimen.SpecimenType.Id;
        specimen.LaboratoryId = specimen.Laboratory.Id;

        if (status is not SpecimenStatus.Taken and not SpecimenStatus.EnteredInError and not SpecimenStatus.Lost)
        {
            specimen.SentAtUtc = taken.AddHours(2);
            specimen.SentByUserId = Nurse.UserId;
        }

        if (status is SpecimenStatus.Received or SpecimenStatus.Reported)
        {
            specimen.ReceivedAtUtc = taken.AddHours(6);
            specimen.ReceivedByUserId = LabStaff.UserId;
        }

        if (status == SpecimenStatus.Reported)
        {
            specimen.ReportedAtUtc = taken.AddHours(20);
            specimen.ReportedByUserId = LabStaff.UserId;
            specimen.ReportAttachmentId = Guid.NewGuid();
        }

        if (status is SpecimenStatus.RejectedByLab or SpecimenStatus.Lost or SpecimenStatus.EnteredInError)
        {
            specimen.ClosedAtUtc = taken.AddHours(8);
            specimen.ClosedByUserId = LabStaff.UserId;
            specimen.ClosedDetail = "Synthetic detail.";
            if (status == SpecimenStatus.RejectedByLab)
                specimen.RejectionReason = SpecimenRejectionReason.Leaked;
        }

        return specimen;
    }
}
