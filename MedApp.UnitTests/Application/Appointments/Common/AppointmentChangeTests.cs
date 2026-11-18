using System.Text.Json;
using FluentAssertions;
using MedApp.Application.Appointments.Common;
using MedApp.Domain.Appointments;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using PathwayData = MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Common;

[TestFixture]
public sealed class AppointmentChangeTests
{
    private const string PathwayTitle = "Synthetic pathway";
    private const string StageName = "Consultation";

    private static AppointmentChange ChangeOf(Appointment appointment, string? staffUsername = StaffUsername) =>
        new(appointment, PathwayData.Staff, Now, staffUsername, PathwayTitle, StageName);

    private static JsonElement Details(AppointmentEvent entry) => JsonDocument.Parse(entry.Details).RootElement;

    [Test]
    public void Request_ByHand_RecordsARequestWithNoDetails()
    {
        var appointment = CreateAppointment(AppointmentStatus.Requested);
        var change = ChangeOf(appointment, null);

        change.Request();

        var entry = change.Events.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AppointmentEventAction.Requested);
        entry.Details.Should().Be("{}");
        entry.UserId.Should().Be(PathwayData.Staff.UserId);
        entry.Username.Should().Be(PathwayData.Staff.Username);
        entry.AtUtc.Should().Be(Now);
        entry.StaffUserId.Should().BeNull();
        entry.StaffUsername.Should().BeNull();
        entry.StartUtc.Should().BeNull();
        appointment.LastUpdated.Should().Be(Now);
    }

    [Test]
    public void Request_ByTheApp_RecordsItAsAutomatic()
    {
        var change = ChangeOf(CreateAppointment(AppointmentStatus.Requested), null);

        change.Request(automatic: true);

        change.Events.Single().Details.Should().Be("{\"automatic\":true}");
    }

    [Test]
    public void Book_Requested_BooksItAndRecordsWhoAndWhen()
    {
        var appointment = CreateAppointment(AppointmentStatus.Requested);
        var change = ChangeOf(appointment, null);

        change.Book(StaffUserId, StaffUsername, At(NextMonday, 10), At(NextMonday, 10, 30));

        appointment.Status.Should().Be(AppointmentStatus.Booked);
        appointment.StaffUserId.Should().Be(StaffUserId);
        appointment.StartUtc.Should().Be(At(NextMonday, 10));
        appointment.EndUtc.Should().Be(At(NextMonday, 10, 30));
        appointment.BookedAtUtc.Should().Be(Now);
        appointment.BookedByUserId.Should().Be(PathwayData.Staff.UserId);
        var entry = change.Events.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AppointmentEventAction.Booked);
        entry.StaffUserId.Should().Be(StaffUserId);
        entry.StaffUsername.Should().Be(StaffUsername);
        entry.StartUtc.Should().Be(At(NextMonday, 10));
        entry.EndUtc.Should().Be(At(NextMonday, 10, 30));
        entry.Details.Should().Be("{}");
    }

    [Test]
    public void Book_Booked_ReschedulesItRecordingTheTimeAndStaffBefore()
    {
        var appointment = CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10));
        var bookedAt = appointment.BookedAtUtc;
        var change = ChangeOf(appointment);

        change.Book(OtherStaffUserId, OtherStaffUsername, At(NextMonday, 14), At(NextMonday, 14, 45));

        appointment.Status.Should().Be(AppointmentStatus.Booked);
        appointment.StaffUserId.Should().Be(OtherStaffUserId);
        appointment.StartUtc.Should().Be(At(NextMonday, 14));
        appointment.BookedAtUtc.Should().Be(bookedAt);
        var entry = change.Events.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AppointmentEventAction.Rescheduled);
        entry.StaffUsername.Should().Be(OtherStaffUsername);
        entry.StartUtc.Should().Be(At(NextMonday, 14));
        var details = Details(entry);
        details.GetProperty("previousStartUtc").GetDateTime().Should().Be(At(NextMonday, 10));
        details.GetProperty("previousEndUtc").GetDateTime().Should().Be(At(NextMonday, 10, 30));
        details.GetProperty("previousStaffUserId").GetGuid().Should().Be(StaffUserId);
        details.GetProperty("previousStaffUsername").GetString().Should().Be(StaffUsername);
    }

    [Test]
    public void Book_BookedToWhereItAlreadyIs_ChangesNothing()
    {
        var appointment = CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10));
        var lastUpdated = appointment.LastUpdated;
        var change = ChangeOf(appointment);

        change.Book(StaffUserId, StaffUsername, At(NextMonday, 10), At(NextMonday, 10, 30));

        change.HasChanges.Should().BeFalse();
        appointment.LastUpdated.Should().Be(lastUpdated);
    }

    [Test]
    public void Book_IntoASessionsSlot_KeepsTheSessionAndRoom_AndRecordsTheirIds()
    {
        var appointment = CreateAppointment(AppointmentStatus.Requested);
        var change = ChangeOf(appointment, null);
        var sessionId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        change.Book(StaffUserId, StaffUsername, At(NextMonday, 9), At(NextMonday, 9, 20), new AppointmentPlace(sessionId, roomId));

        appointment.ClinicSessionId.Should().Be(sessionId);
        appointment.RoomId.Should().Be(roomId);
        var details = Details(change.Events.Single());
        details.GetProperty("clinicSessionId").GetGuid().Should().Be(sessionId);
        details.GetProperty("roomId").GetGuid().Should().Be(roomId);
    }

    [Test]
    public void Book_MovedOutOfASession_LeavesItAndRecordsWhereItWas()
    {
        var appointment = CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10));
        var sessionId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        appointment.ClinicSessionId = sessionId;
        appointment.RoomId = roomId;
        var change = ChangeOf(appointment);

        change.Book(StaffUserId, StaffUsername, At(NextMonday, 14), At(NextMonday, 14, 30));

        appointment.ClinicSessionId.Should().BeNull();
        appointment.RoomId.Should().BeNull();
        var details = Details(change.Events.Single());
        details.GetProperty("previousClinicSessionId").GetGuid().Should().Be(sessionId);
        details.GetProperty("previousRoomId").GetGuid().Should().Be(roomId);
        details.TryGetProperty("clinicSessionId", out _).Should().BeFalse();
    }

    [Test]
    public void Book_SameTimeIntoAnotherRoom_IsAMove()
    {
        var appointment = CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10));
        var change = ChangeOf(appointment);

        change.Book(StaffUserId, StaffUsername, At(NextMonday, 10), At(NextMonday, 10, 30), new AppointmentPlace(null, Guid.NewGuid()));

        change.Events.Should().ContainSingle().Which.Action.Should().Be(AppointmentEventAction.Rescheduled);
    }

    [Test]
    public void RecordOutcome_Booked_MarksItAndRecordsWhoAndWhen()
    {
        var appointment = CreateAppointment(AppointmentStatus.Booked, Now.AddHours(-1));
        var change = ChangeOf(appointment);

        change.RecordOutcome(AppointmentStatus.Attended);

        appointment.Status.Should().Be(AppointmentStatus.Attended);
        appointment.OutcomeAtUtc.Should().Be(Now);
        appointment.OutcomeByUserId.Should().Be(PathwayData.Staff.UserId);
        var entry = change.Events.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AppointmentEventAction.Attended);
        entry.StaffUsername.Should().Be(StaffUsername);
        entry.Details.Should().Be("{\"previousStatus\":\"Booked\"}");
    }

    [Test]
    public void RecordOutcome_Correction_RecordsTheNewOutcomeAndTheOldOne()
    {
        var appointment = CreateAppointment(AppointmentStatus.Attended, Now.AddHours(-1));
        var change = ChangeOf(appointment);

        change.RecordOutcome(AppointmentStatus.DidNotAttend);

        appointment.Status.Should().Be(AppointmentStatus.DidNotAttend);
        change.Events.Single().Action.Should().Be(AppointmentEventAction.DidNotAttend);
        change.Events.Single().Details.Should().Be("{\"previousStatus\":\"Attended\"}");
    }

    [Test]
    public void RecordOutcome_SameAsBefore_ChangesNothing()
    {
        var change = ChangeOf(CreateAppointment(AppointmentStatus.Attended, Now.AddHours(-1)));

        change.RecordOutcome(AppointmentStatus.Attended);

        change.HasChanges.Should().BeFalse();
    }

    [Test]
    public void RecordOutcome_NotAnOutcome_Throws()
    {
        var change = ChangeOf(CreateAppointment(AppointmentStatus.Booked, Now.AddHours(-1)));

        var act = () => change.RecordOutcome(AppointmentStatus.Cancelled);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void Cancel_Booked_CancelsItWithTheReason()
    {
        var appointment = CreateAppointment(AppointmentStatus.Booked);
        var change = ChangeOf(appointment);

        change.Cancel("The patient asked to rearrange.");

        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
        appointment.CancelReason.Should().Be("The patient asked to rearrange.");
        var entry = change.Events.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AppointmentEventAction.Cancelled);
        entry.Details.Should().Be("{\"reason\":\"The patient asked to rearrange.\",\"automatic\":false}");
        entry.StartUtc.Should().Be(appointment.StartUtc);
    }

    [Test]
    public void Cancel_ByTheApp_RecordsItAsAutomatic()
    {
        var change = ChangeOf(CreateAppointment(AppointmentStatus.Requested), null);

        change.Cancel("The stage was finished.", automatic: true);

        Details(change.Events.Single()).GetProperty("automatic").GetBoolean().Should().BeTrue();
    }

    [Test]
    public void UpdateNotes_Changed_RecordsTheOldAndNewNotes()
    {
        var appointment = CreateAppointment(AppointmentStatus.Booked);
        appointment.Notes = "Old synthetic notes.";
        var change = ChangeOf(appointment);

        change.UpdateNotes("New synthetic notes.");

        appointment.Notes.Should().Be("New synthetic notes.");
        var entry = change.Events.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AppointmentEventAction.NotesUpdated);
        entry.Details.Should().Be("{\"from\":\"Old synthetic notes.\",\"to\":\"New synthetic notes.\"}");
    }

    [Test]
    public void UpdateNotes_Unchanged_ChangesNothing()
    {
        var appointment = CreateAppointment(AppointmentStatus.Booked);
        appointment.Notes = "Synthetic notes.";
        var change = ChangeOf(appointment);

        change.UpdateNotes("Synthetic notes.");

        change.HasChanges.Should().BeFalse();
    }

    [Test]
    public void Events_SeveralInOneChange_AreATickApartInOrder()
    {
        var appointment = CreateAppointment(AppointmentStatus.Requested);
        var change = ChangeOf(appointment, null);

        change.Book(StaffUserId, StaffUsername, At(NextMonday, 10), At(NextMonday, 10, 30));
        change.UpdateNotes("Synthetic notes.");

        change.Events.Select(e => (e.Action, e.AtUtc)).Should().Equal(
            (AppointmentEventAction.Booked, Now),
            (AppointmentEventAction.NotesUpdated, Now.AddTicks(1)));
        change.Events.Select(e => e.Id).Should().OnlyHaveUniqueItems();
    }

    [Test]
    public void Events_ForAPathwayStage_CopyTheTitleAndStageGiven()
    {
        var appointment = CreateAppointment(AppointmentStatus.Requested, stageId: Guid.NewGuid());
        appointment.PathwayId = Guid.NewGuid();
        var change = ChangeOf(appointment, null);

        change.Request();

        var entry = change.Events.Single();
        entry.AppointmentId.Should().Be(appointment.Id);
        entry.PatientId.Should().Be(appointment.PatientId);
        entry.PathwayId.Should().Be(appointment.PathwayId);
        entry.PathwayTitle.Should().Be(PathwayTitle);
        entry.StageName.Should().Be(StageName);
    }

    [Test]
    public void Events_ForAPathwayStage_TakeTheTitleAndStageFromTheAppointmentWhenNotGiven()
    {
        var pathway = PathwayData.CreatePathway();
        var appointment = CreateAppointment(AppointmentStatus.Requested, stageId: pathway.StageAt(1).Id);
        appointment.PathwayId = pathway.Id;
        appointment.Pathway = pathway;
        appointment.Stage = pathway.StageAt(1);
        var change = new AppointmentChange(appointment, PathwayData.Staff, Now);

        change.Request();

        change.Events.Single().PathwayTitle.Should().Be(pathway.Title);
        change.Events.Single().StageName.Should().Be(PathwayData.ConsultationStage);
    }

    [Test]
    public void Events_NotForAPathway_HaveNoTitleOrStage()
    {
        var change = ChangeOf(CreateAppointment(AppointmentStatus.Requested), null);

        change.Request();

        change.Events.Single().PathwayId.Should().BeNull();
        change.Events.Single().PathwayTitle.Should().BeEmpty();
        change.Events.Single().StageName.Should().BeNull();
    }

    [Test]
    public void Events_ForAPathwayStageNotLoadedOrGiven_HaveAnEmptyTitleAndNoStageName()
    {
        var appointment = CreateAppointment(AppointmentStatus.Requested, stageId: Guid.NewGuid());
        appointment.PathwayId = Guid.NewGuid();
        var change = new AppointmentChange(appointment, PathwayData.Staff, Now);

        change.Request();

        change.Events.Single().PathwayTitle.Should().BeEmpty();
        change.Events.Single().StageName.Should().BeNull();
    }

    [Test]
    public void Book_Requested_RecordsWhoItIsWithAfterTheChange()
    {
        var appointment = CreateAppointment(AppointmentStatus.Requested);
        var change = ChangeOf(appointment, null);

        change.Book(OtherStaffUserId, OtherStaffUsername, At(NextMonday, 11), At(NextMonday, 11, 30));

        var entry = change.Events.Single();
        entry.StaffUserId.Should().Be(OtherStaffUserId);
        entry.StaffUsername.Should().Be(OtherStaffUsername);
        entry.StartUtc.Should().Be(At(NextMonday, 11));
        entry.EndUtc.Should().Be(At(NextMonday, 11, 30));
        entry.Details.Should().Be("{}");
    }

    [Test]
    public void Appointment_IsTheOneChanged()
    {
        var appointment = CreateAppointment();

        ChangeOf(appointment).Appointment.Should().BeSameAs(appointment);
        ChangeOf(appointment).HasChanges.Should().BeFalse();
    }

    // RecordAfterEvent

    [Test]
    public void RecordAfterEvent_NewAppointment_SetsEverythingAndRecordsOneEntryWithTheOutcome()
    {
        var appointment = CreateAppointment(AppointmentStatus.Requested);
        var change = ChangeOf(appointment, null);
        var start = At(Today.AddDays(-3), 10);

        change.RecordAfterEvent(OtherStaffUserId, OtherStaffUsername, start, start.AddMinutes(20), AppointmentStatus.DidNotAttend);

        appointment.Status.Should().Be(AppointmentStatus.DidNotAttend);
        appointment.IsRecordedAfterEvent.Should().BeTrue();
        appointment.StaffUserId.Should().Be(OtherStaffUserId);
        appointment.StartUtc.Should().Be(start);
        appointment.EndUtc.Should().Be(start.AddMinutes(20));
        appointment.BookedAtUtc.Should().Be(Now);
        appointment.BookedByUserId.Should().Be(PathwayData.Staff.UserId);
        appointment.OutcomeAtUtc.Should().Be(Now);
        appointment.OutcomeByUserId.Should().Be(PathwayData.Staff.UserId);
        appointment.LastUpdated.Should().Be(Now);

        var entry = change.Events.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AppointmentEventAction.RecordedAfterEvent);
        entry.StaffUserId.Should().Be(OtherStaffUserId);
        entry.StaffUsername.Should().Be(OtherStaffUsername);
        entry.StartUtc.Should().Be(start);
        entry.Username.Should().Be(PathwayData.Staff.Username);
        Details(entry).GetProperty("outcome").GetString().Should().Be("DidNotAttend");
        Details(entry).TryGetProperty("previousStatus", out _).Should().BeFalse();
    }

    [Test]
    public void RecordAfterEvent_StagesVisitInNeedsBooking_RecordsItWasRequested()
    {
        var appointment = CreateAppointment(AppointmentStatus.Requested);
        var change = ChangeOf(appointment, null);

        change.RecordAfterEvent(StaffUserId, StaffUsername, At(Today, 8), At(Today, 8, 30), AppointmentStatus.Attended, wasRequested: true);

        var details = Details(change.Events.Single());
        details.GetProperty("outcome").GetString().Should().Be("Attended");
        details.GetProperty("previousStatus").GetString().Should().Be("Requested");
    }

    [TestCase(AppointmentStatus.Booked)]
    [TestCase(AppointmentStatus.Cancelled)]
    public void RecordAfterEvent_NotAnOutcome_Throws(AppointmentStatus outcome)
    {
        var change = ChangeOf(CreateAppointment(AppointmentStatus.Requested));

        var act = () => change.RecordAfterEvent(StaffUserId, StaffUsername, At(Today, 8), At(Today, 8, 30), outcome);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestCase(AppointmentStatus.Booked)]
    [TestCase(AppointmentStatus.Attended)]
    [TestCase(AppointmentStatus.Cancelled)]
    public void RecordAfterEvent_AnAppointmentAlreadyUnderWay_Throws(AppointmentStatus status)
    {
        var change = ChangeOf(CreateAppointment(status));

        var act = () => change.RecordAfterEvent(StaffUserId, StaffUsername, At(Today, 8), At(Today, 8, 30), AppointmentStatus.Attended);

        act.Should().Throw<InvalidOperationException>();
        change.HasChanges.Should().BeFalse();
    }

    [Test]
    public void MarkEnteredInError_KeepsTheReasonAndTheOutcomeItHad()
    {
        var appointment = CreateAppointment(AppointmentStatus.Attended);
        appointment.IsRecordedAfterEvent = true;
        var change = ChangeOf(appointment);

        change.MarkEnteredInError("Wrong patient.");

        appointment.Status.Should().Be(AppointmentStatus.EnteredInError);
        appointment.CancelReason.Should().Be("Wrong patient.");
        appointment.LastUpdated.Should().Be(Now);
        var entry = change.Events.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AppointmentEventAction.EnteredInError);
        Details(entry).GetProperty("reason").GetString().Should().Be("Wrong patient.");
        Details(entry).GetProperty("previousStatus").GetString().Should().Be("Attended");
    }
}
