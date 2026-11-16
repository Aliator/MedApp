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
        var exported = export.Procedures[1];
        exported.Title.Should().Be(later.Title);
        exported.TemplateName.Should().Be(TemplateName);
        exported.Status.Should().Be("Completed");
        exported.Priority.Should().Be(later.Priority.ToString());
        exported.DueDate.Should().Be(DueDates.ToUkDate(later.DueDateUtc));
        exported.Notes.Should().Be(later.Notes);
        exported.AssignedTo.Should().Equal(ProcedureMapper.DeletedUser);
        exported.Stages.Select(s => s.Order).Should().Equal(1, 2, 3);
        exported.Stages[0].Name.Should().Be(later.Stages[0].Name);
        exported.Stages[0].FinishedAtUtc.Should().Be(later.Stages[0].CompletedAtUtc);
        exported.Stages[0].StartedBy.Should().NotBeNull();
        export.Procedures[0].Stages.Should().BeEmpty();
        export.Procedures[0].AssignedTo.Should().BeEmpty();

        var history = export.ProcedureHistory.Should().HaveCount(2).And.Subject.First();
        history.Username.Should().Be(Staff.Username);
        history.Action.Should().Be("Created");
        history.ProcedureTitle.Should().Be(record.ProcedureHistory[0].ProcedureTitle);
        history.Details.Should().Be(record.ProcedureHistory[0].Details);
        var access = export.AccessLog.Should().ContainSingle().Subject;
        access.Username.Should().Be(Staff.Username);
        access.Action.Should().Be("PatientViewed");
        access.AtUtc.Should().Be(record.Activity[0].AtUtc);

        var notification = export.Notifications[1];
        notification.Kind.Should().Be(nameof(NotificationKind.ProcedureAssigned));
        notification.Username.Should().Be(ProcedureMapper.DeletedUser, "the lookup didn't know them");
        notification.Title.Should().Be(record.Notifications[1].Title);
        notification.Message.Should().Be(record.Notifications[1].Message);
        notification.ProcedureId.Should().Be(earlier.Id);
        notification.CreatedAtUtc.Should().Be(record.Notifications[1].CreatedAtUtc);
    }

    [Test]
    public void ToResponse_NamesStaffByUsernameOnly_LeavingOutTheirIdsAddressesAndReadTimes()
    {
        var record = PrivacyTestConstants.CreateRecord(null, CreateProcedure(Status.InProgress, finishedStages: 1));
        record.Notifications[0].ReadAtUtc = Now;
        var entry = record.Activity[0];
        var withAddress = new MedApp.Domain.Activity.ActivityEntry
        {
            Id = entry.Id, AtUtc = entry.AtUtc, Action = entry.Action, UserId = entry.UserId, Username = entry.Username,
            IpAddress = "192.0.2.77", PatientId = entry.PatientId
        };
        var withAddressRecord = new PatientRecord
        {
            Patient = record.Patient,
            Procedures = record.Procedures,
            ProcedureHistory = record.ProcedureHistory,
            Activity = [withAddress],
            Notifications = record.Notifications
        };

        var json = System.Text.Json.JsonSerializer.Serialize(
            PatientExportMapper.ToResponse(withAddressRecord, Usernames, Now, "exporter"),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        json.Should().NotContain("192.0.2.77").And.NotContain("ipAddress").And.NotContain("userId").And.NotContain("readAtUtc");
        json.Should().NotContain(Staff.UserId.ToString()).And.NotContain(Admin.UserId.ToString());
        json.Should().Contain(Staff.Username);
    }

    [Test]
    public void ToResponse_KnownRecipient_IsNamed()
    {
        var record = PrivacyTestConstants.CreateRecord(null, CreateProcedure());

        var export = PatientExportMapper.ToResponse(record, Usernames, Now, "exporter");

        export.Notifications.Single().Username.Should().Be(Staff.Username);
    }

    [Test]
    public void ToResponse_MonitoringNotification_IsLeftOut_WithItsAdmin()
    {
        var procedure = CreateProcedure();
        var record = PrivacyTestConstants.CreateRecord(null, procedure);
        var monitoringAdmin = Guid.NewGuid();
        var withMonitoring = new PatientRecord
        {
            Patient = record.Patient,
            Procedures = record.Procedures,
            ProcedureHistory = record.ProcedureHistory,
            Activity = record.Activity,
            Notifications =
            [
                .. record.Notifications,
                new Notification
                {
                    Id = Guid.NewGuid(), UserId = monitoringAdmin, Kind = NotificationKind.MonitoredStageFinished,
                    Title = "Stage finished", Message = "test.staff finished a stage on a Medium priority Test template procedure.",
                    ProcedureId = procedure.Id, CreatedAtUtc = Now,
                },
            ],
        };

        var export = PatientExportMapper.ToResponse(withMonitoring, Usernames, Now, "exporter");

        export.Notifications.Should().ContainSingle().Which.Kind.Should().Be(nameof(NotificationKind.ProcedureAssigned));
        PatientExportMapper.UserIds(withMonitoring).Should().NotContain(monitoringAdmin);
        // Anonymisation still finds it: it's in the record, only the export leaves it out.
        withMonitoring.Notifications.Should().HaveCount(2);
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
