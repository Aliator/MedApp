using FluentAssertions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Notifications;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.UnitTests.Application.Patients.Common;

[TestFixture]
public sealed class PatientExportMapperTests
{
    [Test]
    public void UserIds_AreEveryoneNamedOnProceduresAndNotifications_OnceEach()
    {
        var procedure = CreateProcedure(Status.InProgress, finishedStages: 1);
        var record = PrivacyTestConstants.CreateRecord(null, procedure);

        PatientExportMapper.UserIds(record).Should().BeEquivalentTo([Staff.UserId, Admin.UserId]);
    }

    [Test]
    public void ToResponse_Record_HasEveryPart()
    {
        var later = CreateProcedure(Status.Completed, finishedStages: 3);
        var earlier = new PatientProcedure
        {
            Id = Guid.NewGuid(),
            Title = "Earlier",
            TemplateName = TemplateName,
            Status = Status.Cancelled,
            DueDateUtc = Now,
            CreatedAt = later.CreatedAt.AddDays(-1),
            LastUpdated = Now
        };
        var record = PrivacyTestConstants.CreateRecord(null, later, earlier);
        record.Notifications[1].ReadAtUtc = Now;
        var usernames = new Dictionary<Guid, string> { [Admin.UserId] = Admin.Username };

        var export = PatientExportMapper.ToResponse(record, usernames, Now, "exporter");

        export.ExportedAtUtc.Should().Be(Now);
        export.ExportedBy.Should().Be("exporter");
        export.Patient.Id.Should().Be(record.Patient.Id);
        export.Procedures.Select(p => p.Id).Should().Equal(earlier.Id, later.Id);
        export.Procedures.Should().AllSatisfy(p => p.PatientName.Should().Be(PatientMapper.FullName(record.Patient)));
        export.Procedures[1].Stages.Should().HaveCount(3);
        export.ProcedureHistory.Should().HaveCount(2);
        export.AccessLog.Should().ContainSingle().Which.PatientName.Should().Be(PatientMapper.FullName(record.Patient));

        var notification = export.Notifications[1];
        notification.Id.Should().Be(record.Notifications[1].Id);
        notification.Kind.Should().Be(nameof(NotificationKind.ProcedureAssigned));
        notification.Username.Should().Be(ProcedureMapper.DeletedUser, "the lookup didn't know them");
        notification.Title.Should().Be(record.Notifications[1].Title);
        notification.Message.Should().Be(record.Notifications[1].Message);
        notification.ProcedureId.Should().Be(earlier.Id);
        notification.CreatedAtUtc.Should().Be(record.Notifications[1].CreatedAtUtc);
        notification.ReadAtUtc.Should().Be(Now);
    }

    [Test]
    public void ToResponse_KnownRecipient_IsNamed()
    {
        var record = PrivacyTestConstants.CreateRecord(null, CreateProcedure());

        var export = PatientExportMapper.ToResponse(record, Usernames, Now, "exporter");

        export.Notifications.Single().Username.Should().Be(Staff.Username);
    }

    [Test]
    public void ToExported_CopiesEveryField()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.RestrictedAtUtc = PrivacyTestConstants.FlaggedAtUtc;
        patient.RestrictionReason = "r";
        patient.DisputedAtUtc = PrivacyTestConstants.FlaggedAtUtc;
        patient.DisputeReason = "d";
        patient.AnonymisedAtUtc = PrivacyTestConstants.FlaggedAtUtc;
        patient.RetentionKeptUntil = new DateOnly(2027, 1, 1);
        patient.RetentionKeptReason = "k";

        var exported = PatientExportMapper.ToExported(patient);

        exported.Should().BeEquivalentTo(new
        {
            patient.Id,
            patient.FirstName,
            patient.LastName,
            patient.DateOfBirth,
            patient.Email,
            patient.CreatedAt,
            patient.LastUpdated,
            patient.RestrictedAtUtc,
            patient.RestrictionReason,
            patient.DisputedAtUtc,
            patient.DisputeReason,
            patient.AnonymisedAtUtc,
            patient.RetentionKeptUntil,
            patient.RetentionKeptReason
        });
    }
}
