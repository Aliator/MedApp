using System.Text.Json.Nodes;
using FluentAssertions;
using MedApp.Application.Patients.Common;
using MedApp.Domain.Activity;
using MedApp.Domain.Appointments;
using MedApp.Domain.Notifications;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.UnitTests.Application.Patients.Common;

[TestFixture]
public sealed class PatientAnonymiserTests
{
    private static readonly DateTime AnonymisedAt = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Apply_Patient_ReplacesTheirDetails_KeepsTheYearOfBirth_AndClearsTheFlags()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.DateOfBirth = new DateOnly(1980, 7, 23);
        patient.DisputedAtUtc = PrivacyTestConstants.FlaggedAtUtc;
        patient.DisputeReason = "Disputes the date of birth";
        patient.RestrictedAtUtc = PrivacyTestConstants.FlaggedAtUtc;
        patient.RestrictionReason = "Restricted";
        patient.RetentionKeptUntil = new DateOnly(2027, 1, 1);
        patient.RetentionKeptReason = "Legal hold";
        var record = PrivacyTestConstants.CreateRecord(patient);

        PatientAnonymiser.Apply(record, AnonymisedAt);

        patient.FirstName.Should().Be(PatientAnonymiser.FirstName);
        patient.LastName.Should().Be(PatientAnonymiser.LastName);
        patient.Email.Should().Be(PatientAnonymiser.Email);
        patient.DateOfBirth.Should().Be(new DateOnly(1980, 1, 1));
        patient.RestrictedAtUtc.Should().BeNull();
        patient.RestrictionReason.Should().BeNull();
        patient.DisputedAtUtc.Should().BeNull();
        patient.DisputeReason.Should().BeNull();
        patient.RetentionKeptUntil.Should().BeNull();
        patient.RetentionKeptReason.Should().BeNull();
        patient.AnonymisedAtUtc.Should().Be(AnonymisedAt);
        patient.LastUpdated.Should().Be(AnonymisedAt);
    }

    [Test]
    public void Apply_Procedures_TakeTheTemplateName_AndLoseTheirNotes()
    {
        var withNotes = CreateProcedure(Status.Completed, finishedStages: 3, title: "Synthetic title naming a condition");
        withNotes.LegacyNotes = PrivacyTestConstants.NotesText;
        var note = withNotes.AddNote(text: PrivacyTestConstants.NotesText, version: 2);
        var noTemplate = CreateProcedure(Status.Cancelled, finishedStages: 0);
        noTemplate.TemplateName = " ";
        var record = PrivacyTestConstants.WithNoteVersions(
            PrivacyTestConstants.CreateRecord(null, withNotes, noTemplate),
            NoteVersion(withNotes, note.Id, 1, PrivacyTestConstants.NotesText),
            NoteVersion(withNotes, note.Id, 2, PrivacyTestConstants.NotesText + " Edited."),
            // A deleted procedure's note: only its versions remain, found by the patient.
            NoteVersion(withNotes, Guid.NewGuid(), 1, PrivacyTestConstants.NotesText, procedureId: Guid.NewGuid()));

        PatientAnonymiser.Apply(record, AnonymisedAt);

        withNotes.Title.Should().Be(TemplateName);
        withNotes.LegacyNotes.Should().Be(PatientAnonymiser.RemovedText);
        note.Text.Should().Be(PatientAnonymiser.RemovedText);
        record.NoteVersions.Should().HaveCount(3).And.AllSatisfy(v => v.Text.Should().Be(PatientAnonymiser.RemovedText));
        withNotes.Stages.Should().AllSatisfy(s => s.Notes.Should().Be(PatientAnonymiser.RemovedText));
        withNotes.Stages[0].Name.Should().Be(ConsultationStage, "stage names come from definitions, not the patient");
        noTemplate.Title.Should().Be(PatientAnonymiser.ProcedureTitle);
        noTemplate.LegacyNotes.Should().BeEmpty("empty text stays empty");
        noTemplate.Notes.Should().BeEmpty();
        noTemplate.Stages.Should().AllSatisfy(s => s.Notes.Should().BeEmpty());
    }

    private static PatientProcedureNoteVersion NoteVersion(
        PatientProcedure procedure, Guid noteId, int version, string text, Guid? procedureId = null) => new()
    {
        Id = Guid.NewGuid(),
        NoteId = noteId,
        Version = version,
        PatientProcedureId = procedureId ?? procedure.Id,
        PatientId = procedure.PatientId,
        Text = text,
        UserId = Staff.UserId,
        Username = Staff.Username,
        AtUtc = Now.AddDays(-version)
    };

    [Test]
    public void Apply_ProcedureHistory_TakesTheNewTitles_AndADeletedProceduresGetsThePlaceholder()
    {
        var procedure = CreateProcedure(Status.Completed);
        var record = PrivacyTestConstants.CreateRecord(null, procedure);
        var deleted = new PatientProcedureAuditEntry
        {
            Id = Guid.NewGuid(),
            PatientProcedureId = Guid.NewGuid(),
            ProcedureTitle = "Deleted synthetic title",
            PatientId = record.Patient.Id,
            Action = PatientProcedureAuditAction.Deleted,
            Details = """{"status":"Cancelled"}"""
        };
        record = WithHistory(record, deleted);

        PatientAnonymiser.Apply(record, AnonymisedAt);

        record.ProcedureHistory[0].ProcedureTitle.Should().Be(TemplateName);
        deleted.ProcedureTitle.Should().Be(PatientAnonymiser.ProcedureTitle);
        deleted.Details.Should().Be("""{"status":"Cancelled"}""");
    }

    [Test]
    public void ScrubDetails_NotesChange_ReplacesFromAndTo()
    {
        var json = Parse(PatientAnonymiser.ScrubDetails("""{"from":"","to":"New note"}""", isNotesChange: true));

        json["from"]!.GetValue<string>().Should().BeEmpty();
        json["to"]!.GetValue<string>().Should().Be(PatientAnonymiser.RemovedText);
    }

    [Test]
    public void ScrubDetails_OtherChange_KeepsTopLevelFromAndTo()
    {
        var json = Parse(PatientAnonymiser.ScrubDetails("""{"from":"InProgress","to":"Blocked","reason":"Awaiting a relative","automatic":false}""", isNotesChange: false));

        json["from"]!.GetValue<string>().Should().Be("InProgress");
        json["to"]!.GetValue<string>().Should().Be("Blocked");
        json["reason"]!.GetValue<string>().Should().Be(PatientAnonymiser.RemovedText);
        json["automatic"]!.GetValue<bool>().Should().BeFalse();
    }

    [Test]
    public void ScrubDetails_DetailsChange_ReplacesTitleAndNotes_KeepsDueDateAndPriority()
    {
        const string details = """
            {"changes":{"title":{"from":"Old","to":"New"},"notes":{"from":"","to":"Note"},
             "dueDate":{"from":"2026-06-01","to":"2026-06-08"},"priority":{"from":"Low","to":"High"}}}
            """;

        var changes = Parse(PatientAnonymiser.ScrubDetails(details, isNotesChange: false))["changes"]!;

        changes["title"]!["from"]!.GetValue<string>().Should().Be(PatientAnonymiser.RemovedText);
        changes["title"]!["to"]!.GetValue<string>().Should().Be(PatientAnonymiser.RemovedText);
        changes["notes"]!["from"]!.GetValue<string>().Should().BeEmpty();
        changes["notes"]!["to"]!.GetValue<string>().Should().Be(PatientAnonymiser.RemovedText);
        changes["dueDate"]!["to"]!.GetValue<string>().Should().Be("2026-06-08");
        changes["priority"]!["to"]!.GetValue<string>().Should().Be("High");
    }

    [Test]
    public void ScrubDetails_NonStringFreeTextKeys_AreLeftAlone()
    {
        var json = Parse(PatientAnonymiser.ScrubDetails("""{"reason":null,"notes":5}""", isNotesChange: false));

        json["reason"].Should().BeNull();
        json["notes"]!.GetValue<int>().Should().Be(5);
    }

    [TestCase("not json")]
    [TestCase("[1,2]")]
    [TestCase("\"text\"")]
    public void ScrubDetails_NotAnObject_IsEmptyObject(string details)
    {
        PatientAnonymiser.ScrubDetails(details, isNotesChange: true).Should().Be("{}");
    }

    [Test]
    public void Apply_ActivityReasons_AreReplaced()
    {
        var record = PrivacyTestConstants.CreateRecord();
        var restricted = new ActivityEntry { Id = Guid.NewGuid(), Action = ActivityAction.PatientRestricted, Details = """{"reason":"Synthetic reason"}""" };
        record = WithActivity(record, restricted);

        PatientAnonymiser.Apply(record, AnonymisedAt);

        Parse(restricted.Details)["reason"]!.GetValue<string>().Should().Be(PatientAnonymiser.RemovedText);
        record.Activity[0].Details.Should().Be("{}");
    }

    [Test]
    public void Apply_Notifications_TakeTheNewTitles()
    {
        var procedure = CreateProcedure(Status.Completed);
        var record = PrivacyTestConstants.CreateRecord(null, procedure);
        var forDeleted = new Notification { Id = Guid.NewGuid(), Title = "Old", ProcedureId = Guid.NewGuid() };
        var noProcedure = new Notification { Id = Guid.NewGuid(), Title = "Old" };
        record = WithNotifications(record, forDeleted, noProcedure);

        PatientAnonymiser.Apply(record, AnonymisedAt);

        record.Notifications[0].Title.Should().Be(TemplateName);
        forDeleted.Title.Should().Be(PatientAnonymiser.ProcedureTitle);
        noProcedure.Title.Should().Be(PatientAnonymiser.ProcedureTitle);
    }

    [Test]
    public void Apply_MonitoringNotification_KeepsItsFixedTitleAndMessage()
    {
        var procedure = CreateProcedure(Status.Completed);
        var record = PrivacyTestConstants.CreateRecord(null, procedure);
        var monitoring = new Notification
        {
            Id = Guid.NewGuid(),
            Kind = NotificationKind.MonitoredStageFinished,
            Title = "Stage finished",
            Message = "test.staff finished a stage on a Medium priority Test template procedure.",
            ProcedureId = procedure.Id,
        };
        record = WithNotifications(record, monitoring);

        PatientAnonymiser.Apply(record, AnonymisedAt);

        monitoring.Title.Should().Be("Stage finished");
        monitoring.Message.Should().Be("test.staff finished a stage on a Medium priority Test template procedure.");
    }

    private static JsonNode Parse(string json) => JsonNode.Parse(json)!;

    private static PatientRecord WithHistory(PatientRecord record, PatientProcedureAuditEntry entry) => new()
    {
        Patient = record.Patient,
        Procedures = record.Procedures,
        ProcedureHistory = [.. record.ProcedureHistory, entry],
        Activity = record.Activity,
        Appointments = record.Appointments,
        AppointmentHistory = record.AppointmentHistory,
        Notifications = record.Notifications
    };

    private static PatientRecord WithActivity(PatientRecord record, ActivityEntry entry) => new()
    {
        Patient = record.Patient,
        Procedures = record.Procedures,
        ProcedureHistory = record.ProcedureHistory,
        Activity = [.. record.Activity, entry],
        Appointments = record.Appointments,
        AppointmentHistory = record.AppointmentHistory,
        Notifications = record.Notifications
    };

    private static PatientRecord WithNotifications(PatientRecord record, params Notification[] notifications) => new()
    {
        Patient = record.Patient,
        Procedures = record.Procedures,
        ProcedureHistory = record.ProcedureHistory,
        Activity = record.Activity,
        Appointments = record.Appointments,
        AppointmentHistory = record.AppointmentHistory,
        Notifications = [.. record.Notifications, .. notifications]
    };

    [Test]
    public void Apply_Appointments_ReplaceTheirNotesAndCancellationReasons()
    {
        var record = PrivacyTestConstants.CreateRecord(null, CreateProcedure(Status.InProgress));
        var appointment = record.Appointments.Single();
        var booked = new Appointment
        {
            Id = Guid.NewGuid(), PatientId = record.Patient.Id, Status = AppointmentStatus.Booked, Notes = string.Empty,
            CreatedAtUtc = Now, CreatedByUserId = Staff.UserId, LastUpdated = Now
        };
        record = WithAppointments(record, booked);

        PatientAnonymiser.Apply(record, AnonymisedAt);

        appointment.Notes.Should().Be(PatientAnonymiser.RemovedText);
        appointment.CancelReason.Should().Be(PatientAnonymiser.RemovedText);
        booked.Notes.Should().BeEmpty("empty text stays empty");
        booked.CancelReason.Should().BeNull();
    }

    [Test]
    public void Apply_AppointmentHistory_TakesTheNewTitle_AndLosesItsFreeText()
    {
        var procedure = CreateProcedure(Status.InProgress);
        procedure.TemplateName = TemplateName;
        var record = PrivacyTestConstants.CreateRecord(null, procedure);

        PatientAnonymiser.Apply(record, AnonymisedAt);

        record.AppointmentHistory.Should().HaveCount(2);
        record.AppointmentHistory.Should().OnlyContain(e => e.ProcedureTitle == TemplateName);
        var notes = Parse(record.AppointmentHistory.Single(e => e.Action == AppointmentEventAction.NotesUpdated).Details);
        notes["from"]!.GetValue<string>().Should().BeEmpty();
        notes["to"]!.GetValue<string>().Should().Be(PatientAnonymiser.RemovedText);
        var cancelled = Parse(record.AppointmentHistory.Single(e => e.Action == AppointmentEventAction.Cancelled).Details);
        cancelled["reason"]!.GetValue<string>().Should().Be(PatientAnonymiser.RemovedText);
        cancelled["automatic"]!.GetValue<bool>().Should().BeFalse();
    }

    [Test]
    public void Apply_AppointmentHistoryNotForAProcedure_KeepsItsEmptyTitle()
    {
        var record = PrivacyTestConstants.CreateRecord();
        var entry = new AppointmentEvent
        {
            Id = Guid.NewGuid(), AppointmentId = Guid.NewGuid(), PatientId = record.Patient.Id,
            Action = AppointmentEventAction.Requested, UserId = Staff.UserId, Username = Staff.Username, AtUtc = Now
        };
        record = WithAppointmentHistory(record, entry);

        PatientAnonymiser.Apply(record, AnonymisedAt);

        entry.ProcedureTitle.Should().BeEmpty();
    }

    [TestCase(NotificationKind.AppointmentBooked, "Appointment booked", false)]
    [TestCase(NotificationKind.AppointmentRescheduled, "Appointment moved", true)]
    [TestCase(NotificationKind.AppointmentCancelled, "Appointment cancelled", true)]
    public void Apply_AppointmentNotification_KeepsItsFixedTitleAndMessage(NotificationKind kind, string title, bool forProcedure)
    {
        var procedure = CreateProcedure(Status.InProgress);
        procedure.TemplateName = TemplateName;
        var record = PrivacyTestConstants.CreateRecord(null, procedure);
        var notification = new Notification
        {
            Id = Guid.NewGuid(), UserId = Staff.UserId, Kind = kind,
            Title = title, Message = "test.staff moved your appointment to Monday 23 November at 10:00.",
            ProcedureId = forProcedure ? procedure.Id : null, AppointmentId = Guid.NewGuid(), CreatedAtUtc = Now
        };
        record = WithNotifications(record, notification);

        PatientAnonymiser.Apply(record, AnonymisedAt);

        notification.Title.Should().Be(title);
        notification.Message.Should().Be("test.staff moved your appointment to Monday 23 November at 10:00.");
    }

    private static PatientRecord WithAppointments(PatientRecord record, params Appointment[] appointments) => new()
    {
        Patient = record.Patient,
        Procedures = record.Procedures,
        ProcedureHistory = record.ProcedureHistory,
        Activity = record.Activity,
        Appointments = [.. record.Appointments, .. appointments],
        AppointmentHistory = record.AppointmentHistory,
        Notifications = record.Notifications
    };

    private static PatientRecord WithAppointmentHistory(PatientRecord record, params AppointmentEvent[] entries) => new()
    {
        Patient = record.Patient,
        Procedures = record.Procedures,
        ProcedureHistory = record.ProcedureHistory,
        Activity = record.Activity,
        Appointments = record.Appointments,
        AppointmentHistory = [.. record.AppointmentHistory, .. entries],
        Notifications = record.Notifications
    };
}
