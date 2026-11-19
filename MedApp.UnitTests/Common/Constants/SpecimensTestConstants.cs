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
    public const string HistologyName = "Histology";
    public const string BloodsName = "Full blood count";
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

    /// <summary>A histology test (MC13): target 7, limit 10, at risk 2 days before; three required steps and the optional ones.</summary>
    public static MedApp.Domain.Lab.LabTest Histology()
    {
        var id = Guid.Parse("1ab00000-0000-4000-8000-000000000001");
        return new MedApp.Domain.Lab.LabTest
        {
            Id = id,
            Name = HistologyName,
            Kind = MedApp.Domain.Lab.LabTestKind.Histology,
            TargetDays = 7,
            LimitDays = 10,
            AtRiskDays = 2,
            CreatedAt = Now.AddDays(-30),
            LastUpdated = Now.AddDays(-30),
            Steps =
            [
                Step(id, 1, 0, "Grossing", MedApp.Domain.Lab.LabStepKind.Standard),
                Step(id, 2, 1, "Processing", MedApp.Domain.Lab.LabStepKind.Standard),
                Step(id, 3, 2, "Special stains", MedApp.Domain.Lab.LabStepKind.ExtraTests, optional: true),
                Step(id, 4, 3, "Sent for second opinion", MedApp.Domain.Lab.LabStepKind.SentForSecondOpinion, optional: true),
                Step(id, 5, 4, "Second opinion returned", MedApp.Domain.Lab.LabStepKind.SecondOpinionReturned, optional: true),
            ],
        };
    }

    /// <summary>A blood test (MC13) with one step and no optional ones.</summary>
    public static MedApp.Domain.Lab.LabTest Bloods()
    {
        var id = Guid.Parse("1ab00000-0000-4000-8000-000000000002");
        return new MedApp.Domain.Lab.LabTest
        {
            Id = id,
            Name = BloodsName,
            Kind = MedApp.Domain.Lab.LabTestKind.Haematology,
            TargetDays = 2,
            LimitDays = 3,
            AtRiskDays = 1,
            CreatedAt = Now.AddDays(-30),
            LastUpdated = Now.AddDays(-30),
            Steps = [Step(id, 6, 0, "Analysed", MedApp.Domain.Lab.LabStepKind.Standard)],
        };
    }

    private static MedApp.Domain.Lab.LabTestStep Step(
        Guid testId, int n, int position, string name, MedApp.Domain.Lab.LabStepKind kind, bool optional = false) => new()
    {
        Id = Guid.Parse($"1ab50000-0000-4000-8000-{n:D12}"),
        LabTestId = testId,
        Position = position,
        Name = name,
        Kind = kind,
        IsOptional = optional,
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
            Source = SpecimenSource.Clinic,
            RequestingTeamId = pathway.TeamId,
            TeamId = pathway.TeamId,
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
