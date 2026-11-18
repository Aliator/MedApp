using FluentAssertions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Patients.Common;

[TestFixture]
public sealed class PatientExportMapperTests
{
    [Test]
    public void UserIds_AreEveryoneNamedOnPathwaysAndNotifications_OnceEach()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 1);
        var record = PrivacyTestConstants.CreateRecord(null, pathway);

        PatientExportMapper.UserIds(record).Should().BeEquivalentTo([Staff.UserId, Admin.UserId]);
    }

    [Test]
    public void ToResponse_Record_HasEveryPart()
    {
        var later = CreatePathway(Status.Completed, finishedStages: 3);
        var earlier = new PatientPathway
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
        var unknown = new PatientPathwayNote
        {
            Id = Guid.NewGuid(), PatientPathwayId = later.Id, AuthorUserId = null, Text = "Synthetic imported note.",
            CreatedAtUtc = Now.AddDays(-5), Version = 1
        };
        later.Notes.Add(unknown);
        var record = PrivacyTestConstants.WithNoteVersions(
            PrivacyTestConstants.CreateRecord(null, later, earlier),
            new PatientPathwayNoteVersion
            {
                Id = Guid.NewGuid(), NoteId = gone.Id, Version = 1, PatientPathwayId = later.Id, Text = "Synthetic note.",
                UserId = OtherUserId, Username = OtherUsername, AtUtc = Now.AddDays(-3)
            },
            new PatientPathwayNoteVersion
            {
                Id = Guid.NewGuid(), NoteId = unknown.Id, Version = 1, PatientPathwayId = later.Id, Text = unknown.Text,
                UserId = null, Username = string.Empty, AtUtc = Now.AddDays(-5)
            });
        record.Notifications[1].ReadAtUtc = Now;
        var usernames = new Dictionary<Guid, string> { [Admin.UserId] = Admin.Username };

        var export = PatientExportMapper.ToResponse(record, usernames, Now, "exporter");

        export.ExportedAtUtc.Should().Be(Now);
        export.ExportedBy.Should().Be("exporter");
        export.Patient.Id.Should().Be(record.Patient.Id);
        export.Pathways.Select(p => p.Id).Should().Equal(earlier.Id, later.Id);
        var exported = export.Pathways[1];
        exported.Title.Should().Be(later.Title);
        exported.TemplateName.Should().Be(TemplateName);
        exported.Status.Should().Be("Completed");
        exported.Priority.Should().Be(later.Priority.ToString());
        exported.DueDate.Should().Be(DueDates.ToUkDate(later.DueDateUtc));
        exported.Notes.Select(n => (n.Id, n.Author, n.Text, n.Version)).Should().Equal(
            (unknown.Id, PathwayNoteMapper.UnknownAuthor, unknown.Text, 1),
            (gone.Id, PathwayMapper.DeletedUser, gone.Text, 2));
        exported.Notes[1].LastEditedAtUtc.Should().Be(gone.LastEditedAtUtc);
        export.NoteHistory.Select(v => (v.NoteId, v.PathwayId, v.Version, v.Username)).Should().Equal(
            (gone.Id, later.Id, 1, OtherUsername),
            (unknown.Id, later.Id, 1, PathwayNoteMapper.UnknownAuthor));
        export.NoteHistory[0].Text.Should().Be("Synthetic note.");
        exported.Notes.Should().AllSatisfy(n => n.DeletedAtUtc.Should().BeNull());
        export.NoteDeletionRequests.Should().BeEmpty();
        exported.AssignedTo.Should().Equal(PathwayMapper.DeletedUser);
        exported.Stages.Select(s => s.Order).Should().Equal(1, 2, 3);
        exported.Stages[0].Name.Should().Be(later.Stages[0].Name);
        exported.Stages[0].FinishedAtUtc.Should().Be(later.Stages[0].CompletedAtUtc);
        exported.Stages[0].StartedBy.Should().NotBeNull();
        export.Pathways[0].Stages.Should().BeEmpty();
        export.Pathways[0].AssignedTo.Should().BeEmpty();

        var history = export.PathwayHistory.Should().HaveCount(2).And.Subject.First();
        history.Username.Should().Be(Staff.Username);
        history.Action.Should().Be("Created");
        history.PathwayTitle.Should().Be(record.PathwayHistory[0].PathwayTitle);
        history.Details.Should().Be(record.PathwayHistory[0].Details);
        var access = export.AccessLog.Should().ContainSingle().Subject;
        access.Username.Should().Be(Staff.Username);
        access.Action.Should().Be("PatientViewed");
        access.AtUtc.Should().Be(record.Activity[0].AtUtc);

        var notification = export.Notifications[1];
        notification.Kind.Should().Be(nameof(NotificationKind.PathwayAssigned));
        notification.Username.Should().Be(PathwayMapper.DeletedUser, "the lookup didn't know them");
        notification.Title.Should().Be(record.Notifications[1].Title);
        notification.Message.Should().Be(record.Notifications[1].Message);
        notification.PathwayId.Should().Be(earlier.Id);
        notification.CreatedAtUtc.Should().Be(record.Notifications[1].CreatedAtUtc);
    }

    [Test]
    public void ToResponse_NamesStaffByUsernameOnly_LeavingOutTheirIdsAddressesAndReadTimes()
    {
        var record = PrivacyTestConstants.CreateRecord(null, CreatePathway(Status.InProgress, finishedStages: 1));
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
            Pathways = record.Pathways,
            PathwayHistory = record.PathwayHistory,
            Activity = [withAddress],
            Appointments = record.Appointments,
            AppointmentHistory = record.AppointmentHistory, PatientMessages = record.PatientMessages,
            Notifications = record.Notifications,
            NoteVersions = [],
            NoteDeletionRequests = [],
            Attachments = [],
            Referrals = [],
            ReferralHistory = [],
            Letters = [],
            Specimens = [],
            MdtItems = []
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
        var record = PrivacyTestConstants.CreateRecord(null, CreatePathway());

        var export = PatientExportMapper.ToResponse(record, Usernames, Now, "exporter");

        export.Notifications.Single().Username.Should().Be(Staff.Username);
    }

    [Test]
    public void ToResponse_MonitoringNotification_IsLeftOut_WithItsAdmin()
    {
        var pathway = CreatePathway();
        var record = PrivacyTestConstants.CreateRecord(null, pathway);
        var monitoringAdmin = Guid.NewGuid();
        var withMonitoring = new PatientRecord
        {
            Patient = record.Patient,
            Pathways = record.Pathways,
            PathwayHistory = record.PathwayHistory,
            Activity = record.Activity,
            Appointments = record.Appointments,
            AppointmentHistory = record.AppointmentHistory, PatientMessages = record.PatientMessages,
            Notifications =
            [
                .. record.Notifications,
                new Notification
                {
                    Id = Guid.NewGuid(), UserId = monitoringAdmin, Kind = NotificationKind.MonitoredStageFinished,
                    Title = "Stage finished", Message = "test.staff finished a stage on a Medium priority Test template pathway.",
                    PathwayId = pathway.Id, CreatedAtUtc = Now,
                },
            ],
            NoteVersions = [],
            NoteDeletionRequests = [],
            Attachments = [],
            Referrals = [],
            ReferralHistory = [],
            Letters = [],
            Specimens = [],
            MdtItems = []
        };

        var export = PatientExportMapper.ToResponse(withMonitoring, Usernames, Now, "exporter");

        export.Notifications.Should().ContainSingle().Which.Kind.Should().Be(nameof(NotificationKind.PathwayAssigned));
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
        var record = PrivacyTestConstants.CreateRecord(null, CreatePathway(Status.InProgress));
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
        var pathway = CreatePathway(Status.InProgress);
        var record = PrivacyTestConstants.CreateRecord(null, pathway);
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
        exported.PathwayId.Should().Be(pathway.Id);
        exported.StageName.Should().Be(pathway.StageAt(1).Name);
        exported.Status.Should().Be("Cancelled");
        exported.StaffUsername.Should().Be(Admin.Username);
        exported.StartUtc.Should().Be(appointment.StartUtc);
        exported.Notes.Should().Be(PrivacyTestConstants.NotesText);
        exported.CancelReason.Should().Be(PrivacyTestConstants.Reason);
        exported.CreatedBy.Should().Be(Staff.Username);
        exported.BookedBy.Should().Be(Staff.Username);
        exported.OutcomeBy.Should().BeNull();
        exported.IsRecordedAfterEvent.Should().BeFalse();
        export.AppointmentHistory.Select(e => e.Action).Should().Equal("NotesUpdated", "Cancelled");
        export.AppointmentHistory.Should().OnlyContain(e => e.AppointmentId == appointment.Id && e.Username == Staff.Username
                                                            && e.PathwayTitle == pathway.Title);
    }

    [Test]
    public void ToResponse_AppointmentWithADeletedStaffMember_NamesThemAsDeleted()
    {
        var record = PrivacyTestConstants.CreateRecord(null, CreatePathway(Status.InProgress));
        record.Appointments.Single().StaffUserId = Guid.NewGuid();

        var export = PatientExportMapper.ToResponse(record, new Dictionary<Guid, string>(), Now, "exporter");

        export.Appointments.Single().StaffUsername.Should().Be(PathwayMapper.DeletedUser);
        export.Appointments.Single().CreatedBy.Should().Be(PathwayMapper.DeletedUser);
    }

    [Test]
    public void ToResponse_AppointmentNotification_CarriesTheAppointment()
    {
        var record = PrivacyTestConstants.CreateRecord(null, CreatePathway(Status.InProgress));
        var appointmentId = record.Appointments.Single().Id;
        var withAppointment = new PatientRecord
        {
            Patient = record.Patient,
            Pathways = record.Pathways,
            PathwayHistory = record.PathwayHistory,
            Activity = record.Activity,
            Appointments = record.Appointments,
            AppointmentHistory = record.AppointmentHistory, PatientMessages = record.PatientMessages,
            Notifications =
            [
                new Notification
                {
                    Id = Guid.NewGuid(), UserId = Staff.UserId, Kind = NotificationKind.AppointmentBooked, Title = "Appointment",
                    Message = "test.admin booked an appointment with you.", AppointmentId = appointmentId, CreatedAtUtc = Now
                }
            ],
            NoteVersions = [],
            NoteDeletionRequests = [],
            Attachments = [],
            Referrals = [],
            ReferralHistory = [],
            Letters = [],
            Specimens = [],
            MdtItems = []
        };

        var export = PatientExportMapper.ToResponse(withAppointment, Usernames, Now, "exporter");

        var notification = export.Notifications.Single();
        notification.AppointmentId.Should().Be(appointmentId);
        notification.PathwayId.Should().BeNull();
    }

    [Test]
    public void ToResponse_Appointments_HoldNoAccountIds()
    {
        var record = PrivacyTestConstants.CreateRecord(null, CreatePathway(Status.InProgress));
        record.Appointments.Single().StaffUserId = Admin.UserId;

        var json = System.Text.Json.JsonSerializer.Serialize(
            PatientExportMapper.ToResponse(record, Usernames, Now, "exporter"),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        json.Should().NotContain("userId").And.NotContain(Staff.UserId.ToString()).And.NotContain(Admin.UserId.ToString());
    }

    [Test]
    public void ToResponse_HistoryDetails_HoldNoStaffAccountIds_ButKeepTheirUsernames()
    {
        var pathway = CreatePathway(Status.InProgress);
        var record = PrivacyTestConstants.CreateRecord(null, pathway);
        var appointmentEvent = record.AppointmentHistory[0];
        var withIds = new PatientRecord
        {
            Patient = record.Patient,
            Pathways = record.Pathways,
            PathwayHistory =
            [
                .. record.PathwayHistory,
                HistoryEntry(pathway, PatientPathwayAuditAction.Assigned,
                    $$"""{"userId":"{{Staff.UserId}}","username":"{{Staff.Username}}"}"""),
                HistoryEntry(pathway, PatientPathwayAuditAction.Unassigned,
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
                    PathwayId = pathway.Id, PathwayTitle = pathway.Title, Action = AppointmentEventAction.Rescheduled,
                    UserId = Admin.UserId, Username = Admin.Username, AtUtc = Now,
                    Details = $$"""{"previousStartUtc":"2026-11-20T14:00:00Z","previousStaffUserId":"{{Staff.UserId}}","previousStaffUsername":"{{Staff.Username}}"}"""
                }
            ],
            PatientMessages = [],
            Notifications = record.Notifications,
            NoteVersions = [],
            NoteDeletionRequests = [],
            Attachments = [],
            Referrals = [],
            ReferralHistory = [],
            Letters = [],
            Specimens = [],
            MdtItems = []
        };

        var export = PatientExportMapper.ToResponse(withIds, Usernames, Now, "exporter");
        var json = System.Text.Json.JsonSerializer.Serialize(
            export, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        json.Should().NotContainEquivalentOf(Staff.UserId.ToString()).And.NotContainEquivalentOf(Admin.UserId.ToString());
        export.PathwayHistory[1].Details.Should().Be($$"""{"username":"{{Staff.Username}}"}""");
        export.PathwayHistory[2].Details.Should().Be($$"""{"username":"{{Admin.Username}}"}""");
        export.PathwayHistory[0].Details.Should().Be(record.PathwayHistory[0].Details, "it has no IDs, so it's as stored");
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

    [Test]
    public void ToResponse_DeletedNoteAndRequests_AreListedWithWhenWhoAndWhy()
    {
        var pathway = CreatePathway(Status.InProgress);
        var deleted = pathway.AddNote();
        var kept = pathway.AddNote(text: "Synthetic second note.", createdAt: Now.AddDays(-1));
        var approved = pathway.RequestDeletion(deleted);
        new PathwayChange(pathway, Admin, Now).DeleteNote(deleted, NoteDeletionReason.WrongPatient, DeletionDetail, approved.Id);
        approved.Close(NoteDeletionRequestStatus.Approved, Admin.UserId, Admin.Username, Now);
        var declined = pathway.RequestDeletion(kept, new PathwayActor(OtherUserId, OtherUsername));
        declined.Close(NoteDeletionRequestStatus.Declined, Admin.UserId, Admin.Username, Now);
        var record = PrivacyTestConstants.WithNoteDeletionRequests(PrivacyTestConstants.CreateRecord(null, pathway), approved, declined);
        var usernames = new Dictionary<Guid, string> { [Admin.UserId] = Admin.Username };

        PatientExportMapper.UserIds(record).Should().Contain(Admin.UserId);
        var export = PatientExportMapper.ToResponse(record, usernames, Now, "exporter");

        // The deleted note is still exported, saying it was deleted; the kept one isn't.
        export.Pathways.Single().Notes.Should().BeEquivalentTo(
        [
            new
            {
                deleted.Id, Text = NoteText, DeletedAtUtc = (DateTime?)Now, DeletedBy = (string?)Admin.Username,
                DeletionReason = (string?)"WrongPatient", DeletionDetail = (string?)DeletionDetail, IsAboutAnotherPatient = true
            },
            new
            {
                kept.Id, Text = "Synthetic second note.", DeletedAtUtc = (DateTime?)null, DeletedBy = (string?)null,
                DeletionReason = (string?)null, DeletionDetail = (string?)null, IsAboutAnotherPatient = false
            },
        ]);
        export.NoteDeletionRequests.Should().BeEquivalentTo(
        [
            new
            {
                approved.Id, NoteId = deleted.Id, PathwayId = pathway.Id, RequestedBy = Staff.Username,
                approved.RequestedAtUtc, Reason = "WrongPatient", Detail = DeletionDetail, Status = "Approved", DecidedBy = Admin.Username, DecidedAtUtc = (DateTime?)Now
            },
            new
            {
                declined.Id, NoteId = kept.Id, PathwayId = pathway.Id, RequestedBy = OtherUsername,
                declined.RequestedAtUtc, Reason = "WrongPatient", Detail = DeletionDetail, Status = "Declined", DecidedBy = Admin.Username, DecidedAtUtc = (DateTime?)Now
            },
        ], o => o.WithStrictOrdering());
    }

    private static PatientPathwayAuditEntry HistoryEntry(PatientPathway pathway, PatientPathwayAuditAction action, string details) => new()
    {
        Id = Guid.NewGuid(),
        PatientPathwayId = pathway.Id,
        PathwayTitle = pathway.Title,
        PatientId = pathway.PatientId,
        Action = action,
        UserId = Admin.UserId,
        Username = Admin.Username,
        AtUtc = Now,
        Details = details
    };

    [Test]
    public void ToResponse_AnAppointmentRecordedAfterTheEvent_SaysSo()
    {
        var record = PrivacyTestConstants.CreateRecord(null, CreatePathway(Status.InProgress));
        record.Appointments.Single().IsRecordedAfterEvent = true;

        PatientExportMapper.ToResponse(record, new Dictionary<Guid, string>(), Now, "exporter")
            .Appointments.Single().IsRecordedAfterEvent.Should().BeTrue();
    }
}
