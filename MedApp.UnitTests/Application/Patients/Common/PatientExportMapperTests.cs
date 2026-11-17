using FluentAssertions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Appointments;
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
        // A note by someone whose account is gone, and one moved from the old notes field with no known author.
        var gone = later.AddNote(author: OtherUserId, text: "Synthetic note, edited.", createdAt: Now.AddDays(-3), version: 2);
        var unknown = new PatientProcedureNote
        {
            Id = Guid.NewGuid(), PatientProcedureId = later.Id, AuthorUserId = null, Text = "Synthetic imported note.",
            CreatedAtUtc = Now.AddDays(-5), Version = 1
        };
        later.Notes.Add(unknown);
        var record = PrivacyTestConstants.WithNoteVersions(
            PrivacyTestConstants.CreateRecord(null, later, earlier),
            new PatientProcedureNoteVersion
            {
                Id = Guid.NewGuid(), NoteId = gone.Id, Version = 1, PatientProcedureId = later.Id, Text = "Synthetic note.",
                UserId = OtherUserId, Username = OtherUsername, AtUtc = Now.AddDays(-3)
            },
            new PatientProcedureNoteVersion
            {
                Id = Guid.NewGuid(), NoteId = unknown.Id, Version = 1, PatientProcedureId = later.Id, Text = unknown.Text,
                UserId = null, Username = string.Empty, AtUtc = Now.AddDays(-5)
            });
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
        exported.Notes.Select(n => (n.Id, n.Author, n.Text, n.Version)).Should().Equal(
            (unknown.Id, ProcedureNoteMapper.UnknownAuthor, unknown.Text, 1),
            (gone.Id, ProcedureMapper.DeletedUser, gone.Text, 2));
        exported.Notes[1].LastEditedAtUtc.Should().Be(gone.LastEditedAtUtc);
        export.NoteHistory.Select(v => (v.NoteId, v.ProcedureId, v.Version, v.Username)).Should().Equal(
            (gone.Id, later.Id, 1, OtherUsername),
            (unknown.Id, later.Id, 1, ProcedureNoteMapper.UnknownAuthor));
        export.NoteHistory[0].Text.Should().Be("Synthetic note.");
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
            Appointments = record.Appointments,
            AppointmentHistory = record.AppointmentHistory,
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
            Appointments = record.Appointments,
            AppointmentHistory = record.AppointmentHistory,
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

    [Test]
    public void UserIds_IncludeEveryoneNamedOnAppointments()
    {
        var record = PrivacyTestConstants.CreateRecord(null, CreateProcedure(Status.InProgress));
        var appointment = record.Appointments.Single();
        var with = Guid.NewGuid();
        var outcomeBy = Guid.NewGuid();
        appointment.StaffUserId = with;
        appointment.BookedByUserId = Admin.UserId;
        appointment.OutcomeByUserId = outcomeBy;

        PatientExportMapper.UserIds(record).Should().Contain([Staff.UserId, Admin.UserId, with, outcomeBy]);
    }

    [Test]
    public void ToResponse_Appointments_AreExportedWithTheirHistory_StaffByUsername()
    {
        var procedure = CreateProcedure(Status.InProgress);
        var record = PrivacyTestConstants.CreateRecord(null, procedure);
        var appointment = record.Appointments.Single();
        appointment.StaffUserId = Admin.UserId;
        appointment.StartUtc = Now.AddDays(1);
        appointment.EndUtc = Now.AddDays(1).AddMinutes(30);
        appointment.BookedByUserId = Staff.UserId;
        appointment.BookedAtUtc = Now;
        var usernames = new Dictionary<Guid, string> { [Admin.UserId] = Admin.Username, [Staff.UserId] = Staff.Username };

        var export = PatientExportMapper.ToResponse(record, usernames, Now, "exporter");

        var exported = export.Appointments.Should().ContainSingle().Subject;
        exported.Id.Should().Be(appointment.Id);
        exported.ProcedureId.Should().Be(procedure.Id);
        exported.StageName.Should().Be(procedure.StageAt(1).Name);
        exported.Status.Should().Be("Cancelled");
        exported.StaffUsername.Should().Be(Admin.Username);
        exported.StartUtc.Should().Be(appointment.StartUtc);
        exported.Notes.Should().Be(PrivacyTestConstants.NotesText);
        exported.CancelReason.Should().Be(PrivacyTestConstants.Reason);
        exported.CreatedBy.Should().Be(Staff.Username);
        exported.BookedBy.Should().Be(Staff.Username);
        exported.OutcomeBy.Should().BeNull();
        export.AppointmentHistory.Select(e => e.Action).Should().Equal("NotesUpdated", "Cancelled");
        export.AppointmentHistory.Should().OnlyContain(e => e.AppointmentId == appointment.Id && e.Username == Staff.Username
                                                            && e.ProcedureTitle == procedure.Title);
    }

    [Test]
    public void ToResponse_AppointmentWithADeletedStaffMember_NamesThemAsDeleted()
    {
        var record = PrivacyTestConstants.CreateRecord(null, CreateProcedure(Status.InProgress));
        record.Appointments.Single().StaffUserId = Guid.NewGuid();

        var export = PatientExportMapper.ToResponse(record, new Dictionary<Guid, string>(), Now, "exporter");

        export.Appointments.Single().StaffUsername.Should().Be(ProcedureMapper.DeletedUser);
        export.Appointments.Single().CreatedBy.Should().Be(ProcedureMapper.DeletedUser);
    }

    [Test]
    public void ToResponse_AppointmentNotification_CarriesTheAppointment()
    {
        var record = PrivacyTestConstants.CreateRecord(null, CreateProcedure(Status.InProgress));
        var appointmentId = record.Appointments.Single().Id;
        var withAppointment = new PatientRecord
        {
            Patient = record.Patient,
            Procedures = record.Procedures,
            ProcedureHistory = record.ProcedureHistory,
            Activity = record.Activity,
            Appointments = record.Appointments,
            AppointmentHistory = record.AppointmentHistory,
            Notifications =
            [
                new Notification
                {
                    Id = Guid.NewGuid(), UserId = Staff.UserId, Kind = NotificationKind.AppointmentBooked, Title = "Appointment",
                    Message = "test.admin booked an appointment with you.", AppointmentId = appointmentId, CreatedAtUtc = Now
                }
            ]
        };

        var export = PatientExportMapper.ToResponse(withAppointment, Usernames, Now, "exporter");

        var notification = export.Notifications.Single();
        notification.AppointmentId.Should().Be(appointmentId);
        notification.ProcedureId.Should().BeNull();
    }

    [Test]
    public void ToResponse_Appointments_HoldNoAccountIds()
    {
        var record = PrivacyTestConstants.CreateRecord(null, CreateProcedure(Status.InProgress));
        record.Appointments.Single().StaffUserId = Admin.UserId;

        var json = System.Text.Json.JsonSerializer.Serialize(
            PatientExportMapper.ToResponse(record, Usernames, Now, "exporter"),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        json.Should().NotContain("userId").And.NotContain(Staff.UserId.ToString()).And.NotContain(Admin.UserId.ToString());
    }

    [Test]
    public void ToResponse_HistoryDetails_HoldNoStaffAccountIds_ButKeepTheirUsernames()
    {
        var procedure = CreateProcedure(Status.InProgress);
        var record = PrivacyTestConstants.CreateRecord(null, procedure);
        var appointmentEvent = record.AppointmentHistory[0];
        var withIds = new PatientRecord
        {
            Patient = record.Patient,
            Procedures = record.Procedures,
            ProcedureHistory =
            [
                .. record.ProcedureHistory,
                HistoryEntry(procedure, PatientProcedureAuditAction.Assigned,
                    $$"""{"userId":"{{Staff.UserId}}","username":"{{Staff.Username}}"}"""),
                HistoryEntry(procedure, PatientProcedureAuditAction.Unassigned,
                    $$"""{"userId":"{{Admin.UserId}}","username":"{{Admin.Username}}"}"""),
            ],
            Activity =
            [
                new MedApp.Domain.Activity.ActivityEntry
                {
                    Id = Guid.NewGuid(), AtUtc = Now, Action = MedApp.Domain.Activity.ActivityAction.PatientUpdated,
                    UserId = Staff.UserId, Username = Staff.Username, PatientId = record.Patient.Id,
                    Details = $$"""{"fields":["email"],"byUserId":"{{Admin.UserId}}"}"""
                }
            ],
            Appointments = record.Appointments,
            AppointmentHistory =
            [
                new AppointmentEvent
                {
                    Id = Guid.NewGuid(), AppointmentId = appointmentEvent.AppointmentId, PatientId = appointmentEvent.PatientId,
                    ProcedureId = procedure.Id, ProcedureTitle = procedure.Title, Action = AppointmentEventAction.Rescheduled,
                    UserId = Admin.UserId, Username = Admin.Username, AtUtc = Now,
                    Details = $$"""{"previousStartUtc":"2026-11-20T14:00:00Z","previousStaffUserId":"{{Staff.UserId}}","previousStaffUsername":"{{Staff.Username}}"}"""
                }
            ],
            Notifications = record.Notifications
        };

        var export = PatientExportMapper.ToResponse(withIds, Usernames, Now, "exporter");
        var json = System.Text.Json.JsonSerializer.Serialize(
            export, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        json.Should().NotContainEquivalentOf(Staff.UserId.ToString()).And.NotContainEquivalentOf(Admin.UserId.ToString());
        export.ProcedureHistory[1].Details.Should().Be($$"""{"username":"{{Staff.Username}}"}""");
        export.ProcedureHistory[2].Details.Should().Be($$"""{"username":"{{Admin.Username}}"}""");
        export.ProcedureHistory[0].Details.Should().Be(record.ProcedureHistory[0].Details, "it has no IDs, so it's as stored");
        export.AppointmentHistory.Single().Details.Should()
            .Be($$"""{"previousStartUtc":"2026-11-20T14:00:00Z","previousStaffUsername":"{{Staff.Username}}"}""");
        export.AccessLog.Single().Details.Should().Be("""{"fields":["email"]}""");
    }

    [TestCase("""{"userId":"x","username":"a"}""", """{"username":"a"}""")]
    [TestCase("""{"previousStaffUserId":"x","previousStaffUsername":"a"}""", """{"previousStaffUsername":"a"}""")]
    [TestCase("""{"UserID":"x","staffUserIds":["x"],"other":1}""", """{"other":1}""")]
    [TestCase("""{"changes":{"assignee":{"fromUserId":"x","to":"a"}}}""", """{"changes":{"assignee":{"to":"a"}}}""")]
    [TestCase("""{"items":[{"userId":"x","name":"a"},2]}""", """{"items":[{"name":"a"},2]}""")]
    public void WithoutStaffIds_RemovesEveryUserIdProperty_AtAnyDepth(string details, string expected)
    {
        PatientExportMapper.WithoutStaffIds(details).Should().Be(expected);
    }

    [TestCase("{}")]
    [TestCase("""{"template":"Test template","stageCount":3}""")]
    [TestCase("""{ "username" : "a" }""")]
    public void WithoutStaffIds_NoIds_ReturnsTheDetailsAsStored(string details)
    {
        PatientExportMapper.WithoutStaffIds(details).Should().BeSameAs(details);
    }

    [TestCase("not json")]
    [TestCase("[]")]
    [TestCase("\"a string\"")]
    [TestCase("null")]
    public void WithoutStaffIds_NotAJsonObject_IsEmpty(string details)
    {
        PatientExportMapper.WithoutStaffIds(details).Should().Be("{}");
    }

    private static PatientProcedureAuditEntry HistoryEntry(PatientProcedure procedure, PatientProcedureAuditAction action, string details) => new()
    {
        Id = Guid.NewGuid(),
        PatientProcedureId = procedure.Id,
        ProcedureTitle = procedure.Title,
        PatientId = procedure.PatientId,
        Action = action,
        UserId = Admin.UserId,
        Username = Admin.Username,
        AtUtc = Now,
        Details = details
    };
}
