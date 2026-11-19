using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Consent;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>Synthetic consent forms and records, and the people who record them.</summary>
public static class ConsentTestConstants
{
    public const string FormName = "Consent for a skin procedure (synthetic)";
    public const string FormVersion = "3";

    /// <summary>A nurse who records consent.</summary>
    public static readonly PathwayActor Recorder = new(
        Staff.UserId, Staff.Username,
        new HashSet<Permission> { Permission.UseApp, Permission.ViewPatients, Permission.WorkPathways, Permission.RecordConsent });

    /// <summary>An admin: corrects records, but doesn't record consent (it's clinical).</summary>
    public static readonly PathwayActor Corrector = new(Admin.UserId, Admin.Username, Permissions.Admin);

    public static ConsentForm Form(bool archived = false) => new()
    {
        Id = Guid.Parse("c0e50000-0000-4000-8000-000000000001"),
        Name = FormName,
        FormVersion = FormVersion,
        IsArchived = archived,
        CreatedAt = Now.AddDays(-30),
        LastUpdated = Now.AddDays(-30),
    };

    /// <summary>
    /// Valid consent to the pathway's first stage, signed today by the patient, recorded an hour ago by
    /// <see cref="Recorder"/>, at version 1.
    /// </summary>
    public static ConsentRecord CreateRecord(
        PatientPathway? pathway = null,
        ConsentStatus status = ConsentStatus.Valid,
        Guid? scanAttachmentId = null,
        DateTime? recordedAtUtc = null)
    {
        pathway ??= CreatePathway(PatientPathwayStatus.InProgress);
        var form = Form();
        var record = new ConsentRecord
        {
            Id = Guid.NewGuid(),
            PatientId = pathway.PatientId,
            Patient = pathway.Patient,
            PathwayId = pathway.Id,
            Pathway = pathway,
            TeamId = pathway.TeamId,
            StageId = pathway.StageAt(1).Id,
            ConsentFormId = form.Id,
            ConsentForm = form,
            FormName = form.Name,
            FormVersion = form.FormVersion,
            SignedOn = Today,
            SignedBy = ConsentSignedBy.Patient,
            RecordedByUserId = Recorder.UserId,
            RecordedAtUtc = recordedAtUtc ?? Now.AddHours(-1),
            ScanAttachmentId = scanAttachmentId,
            Status = status,
            Version = 1,
        };

        if (status != ConsentStatus.Valid)
        {
            record.StatusDetail = "Synthetic detail.";
            record.StatusChangedAtUtc = Now.AddMinutes(-30);
            record.StatusChangedByUserId = Recorder.UserId;
            if (status == ConsentStatus.Withdrawn)
                record.WithdrawnOn = Today;
        }

        return record;
    }
}
