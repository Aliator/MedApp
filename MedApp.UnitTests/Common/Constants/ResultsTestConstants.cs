using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Attachments;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Results;
using MedApp.Domain.Specimens;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>Synthetic results for the results tests: no real patient, report or clinical text.</summary>
public static class ResultsTestConstants
{
    public const string ExternalTitle = "Synthetic scan report from another service";
    public const string ActionDetail = "Synthetic detail: discussed by phone.";
    public const string ErrorDetail = "Synthetic detail: the report belongs to another patient.";

    /// <summary>A clinician who signs off results (the specimens' requesting clinician, <see cref="SpecimensTestConstants.Nurse"/>'s account).</summary>
    public static readonly PathwayActor Clinician = new(
        Staff.UserId, Staff.Username,
        new HashSet<Permission> { Permission.UseApp, Permission.ViewPatients, Permission.WorkPathways, Permission.SignOffResults });

    /// <summary>Another clinician who signs off results.</summary>
    public static readonly PathwayActor OtherClinician = new(
        Admin.UserId, Admin.Username,
        new HashSet<Permission> { Permission.UseApp, Permission.ViewPatients, Permission.SignOffResults });

    /// <summary>
    /// A specimen's result, reported an hour ago by <see cref="SpecimensTestConstants.LabStaff"/>, for
    /// <see cref="Clinician"/> to sign off; its specimen is Reported.
    /// </summary>
    public static Result CreateResult(
        PatientPathway? pathway = null,
        ResultStatus status = ResultStatus.AwaitingSignOff,
        DateTime? reportedAtUtc = null)
    {
        var specimen = SpecimensTestConstants.CreateSpecimen(pathway, SpecimenStatus.Reported);
        specimen.ReportAttachmentId = Guid.NewGuid();
        return new Result
        {
            Id = Guid.NewGuid(),
            PatientId = specimen.PatientId,
            Patient = specimen.Patient,
            PathwayId = specimen.PathwayId,
            Pathway = specimen.Pathway,
            SpecimenId = specimen.Id,
            Specimen = specimen,
            Source = ResultSource.Specimen,
            Title = $"{SpecimensTestConstants.BiopsyName} {SpecimensTestConstants.Number}",
            ReportAttachmentId = specimen.ReportAttachmentId.Value,
            ReportedAtUtc = reportedAtUtc ?? Now.AddHours(-1),
            ReportedByUserId = SpecimensTestConstants.LabStaff.UserId,
            ResponsibleUserId = Clinician.UserId,
            Status = status,
            Version = 1,
        };
    }

    /// <summary>The report file of <paramref name="result"/>, on its pathway.</summary>
    public static Attachment ReportOf(Result result) => new()
    {
        Id = result.ReportAttachmentId,
        PatientId = result.PatientId,
        PathwayId = result.PathwayId,
        FileName = "synthetic-report.pdf",
        ContentType = "application/pdf",
        SizeBytes = 100,
        Sha256 = new string('0', 64),
        UploadedByUserId = result.ReportedByUserId,
        UploadedByUsername = OtherUsername,
        UploadedAtUtc = result.ReportedAtUtc,
    };
}
