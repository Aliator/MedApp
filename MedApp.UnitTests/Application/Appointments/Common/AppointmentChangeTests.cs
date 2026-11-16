using System.Text.Json;
using FluentAssertions;
using MedApp.Application.Appointments.Common;
using MedApp.Domain.Appointments;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using ProcedureData = MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Common;

[TestFixture]
public sealed class AppointmentChangeTests
{
    private const string ProcedureTitle = "Synthetic procedure";
    private const string StageName = "Consultation";

    private static AppointmentChange ChangeOf(Appointment appointment, string? staffUsername = StaffUsername) =>
        new(appointment, ProcedureData.Staff, Now, staffUsername, ProcedureTitle, StageName);

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
        entry.UserId.Should().Be(ProcedureData.Staff.UserId);
        entry.Username.Should().Be(ProcedureData.Staff.Username);
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
        appointment.BookedByUserId.Should().Be(ProcedureData.Staff.UserId);
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
    public void RecordOutcome_Booked_MarksItAndRecordsWhoAndWhen()
    {
        var appointment = CreateAppointment(AppointmentStatus.Booked, Now.AddHours(-1));
        var change = ChangeOf(appointment);

        change.RecordOutcome(AppointmentStatus.Attended);

        appointment.Status.Should().Be(AppointmentStatus.Attended);
        appointment.OutcomeAtUtc.Should().Be(Now);
        appointment.OutcomeByUserId.Should().Be(ProcedureData.Staff.UserId);
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
    public void Events_ForAProcedureStage_CopyTheTitleAndStageGiven()
    {
        var appointment = CreateAppointment(AppointmentStatus.Requested, stageId: Guid.NewGuid());
        appointment.ProcedureId = Guid.NewGuid();
        var change = ChangeOf(appointment, null);

        change.Request();

        var entry = change.Events.Single();
        entry.AppointmentId.Should().Be(appointment.Id);
        entry.PatientId.Should().Be(appointment.PatientId);
        entry.ProcedureId.Should().Be(appointment.ProcedureId);
        entry.ProcedureTitle.Should().Be(ProcedureTitle);
        entry.StageName.Should().Be(StageName);
    }

    [Test]
    public void Events_ForAProcedureStage_TakeTheTitleAndStageFromTheAppointmentWhenNotGiven()
    {
        var procedure = ProcedureData.CreateProcedure();
        var appointment = CreateAppointment(AppointmentStatus.Requested, stageId: procedure.StageAt(1).Id);
        appointment.ProcedureId = procedure.Id;
        appointment.Procedure = procedure;
        appointment.Stage = procedure.StageAt(1);
        var change = new AppointmentChange(appointment, ProcedureData.Staff, Now);

        change.Request();

        change.Events.Single().ProcedureTitle.Should().Be(procedure.Title);
        change.Events.Single().StageName.Should().Be(ProcedureData.ConsultationStage);
    }

    [Test]
    public void Events_NotForAProcedure_HaveNoTitleOrStage()
    {
        var change = ChangeOf(CreateAppointment(AppointmentStatus.Requested), null);

        change.Request();

        change.Events.Single().ProcedureId.Should().BeNull();
        change.Events.Single().ProcedureTitle.Should().BeEmpty();
        change.Events.Single().StageName.Should().BeNull();
    }

    [Test]
    public void Events_ForAProcedureStageNotLoadedOrGiven_HaveAnEmptyTitleAndNoStageName()
    {
        var appointment = CreateAppointment(AppointmentStatus.Requested, stageId: Guid.NewGuid());
        appointment.ProcedureId = Guid.NewGuid();
        var change = new AppointmentChange(appointment, ProcedureData.Staff, Now);

        change.Request();

        change.Events.Single().ProcedureTitle.Should().BeEmpty();
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
}
