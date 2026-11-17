using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Contracts.Notifications.Responses;
using MedApp.Contracts.Patients.Requests;
using MedApp.Contracts.Patients.Responses;
using MedApp.Contracts.Procedures.NoteDeletionRequests.Requests;
using MedApp.Contracts.Procedures.NoteDeletionRequests.Responses;
using MedApp.Contracts.Procedures.PatientProcedures.Requests;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.Contracts.Procedures.Timeline.Responses;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.Infrastructure.Data;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Procedures;

/// <summary>
/// Deleting a procedure's notes through the API: admins delete one directly, with a reason; staff ask, every admin is
/// told, and one decides from the request (delete or keep), with the requester told. Deleting is soft: the note is
/// hidden from the procedure for everyone, kept in the database and listed in the patient's export as deleted. Neither
/// works on a restricted record, and only admins delete or decide.
/// </summary>
[TestFixture]
public sealed class NoteDeletionTests : IntegrationTest
{
    private const string NoteText = "Synthetic note: written on the wrong record.";
    private const string RequestReason = "Synthetic reason: this belongs to another procedure.";
    private const string AdminReason = "Synthetic reason: confirmed with the author.";

    private HttpClient _admin = null!;
    private StaffAccount _staff = null!;

    [SetUp]
    public async Task SignInAsync()
    {
        _admin = await SignInAsAdminAsync();
        _staff = await CreateStaffAsync(UserRole);
    }

    private static string NoteUrl(Guid procedureId, Guid noteId) => $"{ProcedureUrl(procedureId)}/notes/{noteId}";

    private static string RequestUrl(Guid requestId) => $"/api/procedures/note-deletion-requests/{requestId}";

    private static Task<HttpResponseMessage> DeleteAsync(HttpClient client, Guid procedureId, Guid noteId, string reason = AdminReason) =>
        client.PostAsJsonAsync($"{NoteUrl(procedureId, noteId)}/delete", new DeleteProcedureNoteRequest { Reason = reason });

    private static Task<HttpResponseMessage> RequestAsync(HttpClient client, Guid procedureId, Guid noteId) =>
        client.PostAsJsonAsync($"{NoteUrl(procedureId, noteId)}/deletion-requests", new RequestNoteDeletionRequest { Reason = RequestReason });

    private static Task<HttpResponseMessage> DecideAsync(HttpClient client, Guid requestId, bool deleteNote, string? reason = null) =>
        client.PostAsJsonAsync($"{RequestUrl(requestId)}/decision", new DecideNoteDeletionRequestRequest { DeleteNote = deleteNote, Reason = reason });

    private static async Task<PatientProcedureResponse> GetProcedureAsync(HttpClient client, Guid procedureId) =>
        await (await client.GetAsync(ProcedureUrl(procedureId))).ReadAsAsync<PatientProcedureResponse>();

    private static async Task<List<NotificationResponse>> NotificationsAsync(HttpClient client) =>
        (await (await client.GetAsync("/api/notifications")).ReadAsAsync<NotificationsResponse>()).Items.ToList();

    private async Task<(PatientProcedureResponse Procedure, PatientProcedureNoteResponse Note)> ProcedureWithNoteAsync()
    {
        var procedure = await CreatePatientWithProcedureAsync(_admin);
        var note = await (await _staff.Client.PostAsJsonAsync($"{ProcedureUrl(procedure.Id)}/notes", new AddProcedureNoteRequest { Text = NoteText }))
            .ReadAsAsync<PatientProcedureNoteResponse>(HttpStatusCode.Created);
        return (procedure, note);
    }

    [Test]
    public async Task Staff_CantDeleteReadOrDecide()
    {
        var (procedure, note) = await ProcedureWithNoteAsync();
        var requested = await (await RequestAsync(_staff.Client, procedure.Id, note.Id)).ReadAsAsync<PatientProcedureNoteResponse>(HttpStatusCode.Created);

        await (await DeleteAsync(_staff.Client, procedure.Id, note.Id)).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await (await _staff.Client.GetAsync(RequestUrl(requested.DeletionRequestId!.Value))).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await (await DecideAsync(_staff.Client, requested.DeletionRequestId.Value, true)).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        (await GetProcedureAsync(_staff.Client, procedure.Id)).NoteList.Should().ContainSingle(n => n.Id == note.Id);
    }

    [Test]
    public async Task AdminDeletes_TheNoteIsHiddenForEveryoneButKeptAuditedAndExported()
    {
        var (procedure, note) = await ProcedureWithNoteAsync();

        await (await DeleteAsync(_admin, procedure.Id, note.Id)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        (await GetProcedureAsync(_staff.Client, procedure.Id)).NoteList.Should().BeEmpty();
        (await GetProcedureAsync(_admin, procedure.Id)).NoteList.Should().BeEmpty();
        var (stored, audit) = await RunInScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<MedAppDbContext>();
            return (
                await db.PatientProcedureNotes.AsNoTracking().SingleAsync(n => n.Id == note.Id),
                await db.PatientProcedureAuditEntries.AsNoTracking()
                    .SingleAsync(e => e.PatientProcedureId == procedure.Id && e.Action == PatientProcedureAuditAction.NoteDeleted));
        });
        stored.Should().BeEquivalentTo(new { Text = NoteText, DeletionReason = AdminReason, PendingDeletionRequestId = (Guid?)null });
        stored.DeletedAtUtc.Should().NotBeNull();
        audit.Details.Should().Contain(AdminReason).And.NotContain(NoteText).And.Contain(note.Id.ToString());

        var export = await (await _admin.GetAsync($"/api/patients/{procedure.PatientId}/export")).ReadAsAsync<PatientExportResponse>();
        export.Procedures.Single().Notes.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            note.Id, Text = NoteText, DeletedBy = AdminUsername, DeletionReason = AdminReason
        });

        // Deleting it again, or editing it, is refused.
        await (await DeleteAsync(_admin, procedure.Id, note.Id)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await (await _staff.Client.PutAsJsonAsync(NoteUrl(procedure.Id, note.Id), new EditProcedureNoteRequest { Text = "Synthetic edit.", ExpectedVersion = 1 }))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Timeline_StaffSeeThatANoteWasDeleted_AdminsSeeItsTextAndTheReason()
    {
        var (procedure, note) = await ProcedureWithNoteAsync();
        Clock.Advance(TimeSpan.FromMinutes(1));
        await (await DeleteAsync(_admin, procedure.Id, note.Id)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        var url = $"/api/patients/{procedure.PatientId}/timeline?Kinds=Notes";

        var staffBody = await (await _staff.Client.GetAsync(url)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        var adminPage = await (await _admin.GetAsync(url)).ReadAsAsync<PatientTimelinePageResponse>();

        var raw = await staffBody.Content.ReadAsStringAsync();
        raw.Should().Contain("NoteDeleted").And.NotContain(NoteText).And.NotContain(AdminReason);
        var deleted = adminPage.Items.First();
        deleted.Action.Should().Be("NoteDeleted");
        deleted.Reason.Should().Be(AdminReason);
        deleted.Changes.Single().From.Should().Be(NoteText);
    }

    [Test]
    public async Task DeleteAndRequest_RestrictedRecord_Are409()
    {
        var (procedure, note) = await ProcedureWithNoteAsync();
        await (await _admin.PutAsJsonAsync($"/api/patients/{procedure.PatientId}/restriction",
                new SetPatientFlagRequest { Set = true, Reason = "Synthetic accuracy check", ConfirmOpenProcedures = true }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        await (await DeleteAsync(_admin, procedure.Id, note.Id)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await (await RequestAsync(_staff.Client, procedure.Id, note.Id)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        var reloaded = (await GetProcedureAsync(_admin, procedure.Id)).NoteList.Single();
        reloaded.Should().BeEquivalentTo(new { CanDelete = false, CanRequestDeletion = false });
    }

    [Test]
    public async Task Decide_RestrictedAfterTheRequest_Is409AndTheDialogSaysWhy()
    {
        var (procedure, note) = await ProcedureWithNoteAsync();
        var requested = await (await RequestAsync(_staff.Client, procedure.Id, note.Id)).ReadAsAsync<PatientProcedureNoteResponse>(HttpStatusCode.Created);
        await (await _admin.PutAsJsonAsync($"/api/patients/{procedure.PatientId}/restriction",
                new SetPatientFlagRequest { Set = true, Reason = "Synthetic accuracy check", ConfirmOpenProcedures = true }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        var requestId = requested.DeletionRequestId!.Value;

        await (await DecideAsync(_admin, requestId, true)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        var request = await (await _admin.GetAsync(RequestUrl(requestId))).ReadAsAsync<NoteDeletionRequestResponse>();
        request.Should().BeEquivalentTo(new { Status = "Pending", CanDecide = false });
        request.WhyCantDecide.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task RequestThenDelete_EndToEnd()
    {
        var (procedure, note) = await ProcedureWithNoteAsync();

        // Staff ask: the note shows the request, and asking again names it.
        var requested = await (await RequestAsync(_staff.Client, procedure.Id, note.Id)).ReadAsAsync<PatientProcedureNoteResponse>(HttpStatusCode.Created);
        var requestId = requested.DeletionRequestId!.Value;
        requested.CanRequestDeletion.Should().BeFalse();
        (await GetProcedureAsync(_staff.Client, procedure.Id)).NoteList.Single().DeletionRequestId.Should().Be(requestId);
        (await (await RequestAsync(_admin, procedure.Id, note.Id)).ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict))
            .Title.Should().Contain(_staff.Username);

        // Every admin is told, with a link to the request; the requester isn't.
        var told = (await NotificationsAsync(_admin)).Should().ContainSingle(n => n.Kind == "NoteDeletionRequested").Subject;
        told.Should().BeEquivalentTo(new
        {
            Title = "Note deletion requested",
            Message = $"{_staff.Username} asked to delete a note on a procedure.",
            ProcedureId = (Guid?)procedure.Id,
            NoteDeletionRequestId = (Guid?)requestId
        });
        (await NotificationsAsync(_staff.Client)).Should().NotContain(n => n.Kind == "NoteDeletionRequested");

        // The admin's dialog: the note, the patient and the request.
        var request = await (await _admin.GetAsync(RequestUrl(requestId))).ReadAsAsync<NoteDeletionRequestResponse>();
        request.Should().BeEquivalentTo(new
        {
            Status = "Pending",
            Reason = RequestReason,
            RequestedByUsername = _staff.Username,
            ProcedureId = procedure.Id,
            ProcedureTitle = procedure.Title,
            Patient = new { Id = procedure.PatientId },
            Note = new { note.Id, Text = NoteText, AuthorUsername = _staff.Username },
            CanDecide = true
        });

        // Delete note: hidden, the request approved, the requester told.
        var decided = await (await DecideAsync(_admin, requestId, true)).ReadAsAsync<NoteDeletionRequestResponse>();
        decided.Should().BeEquivalentTo(new
        {
            Status = "Approved",
            DecidedByUsername = AdminUsername,
            CanDecide = false,
            Note = new { DeletionReason = RequestReason, DeletedByUsername = AdminUsername }
        });
        (await GetProcedureAsync(_staff.Client, procedure.Id)).NoteList.Should().BeEmpty();
        (await NotificationsAsync(_staff.Client)).Should().ContainSingle(n => n.Kind == "NoteDeletionApproved")
            .Which.Message.Should().Be($"{AdminUsername} deleted the note you asked to delete.");

        // Deciding it again is refused.
        (await (await DecideAsync(_admin, requestId, false)).ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict))
            .Title.Should().Contain("already deleted this note");

        var export = await (await _admin.GetAsync($"/api/patients/{procedure.PatientId}/export")).ReadAsAsync<PatientExportResponse>();
        export.NoteDeletionRequests.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Id = requestId, NoteId = note.Id, RequestedBy = _staff.Username, Reason = RequestReason, Status = "Approved", DecidedBy = AdminUsername
        });
    }

    [Test]
    public async Task RequestThenKeep_LeavesTheNoteAndItCanBeAskedForAgain()
    {
        var (procedure, note) = await ProcedureWithNoteAsync();
        var requested = await (await RequestAsync(_staff.Client, procedure.Id, note.Id)).ReadAsAsync<PatientProcedureNoteResponse>(HttpStatusCode.Created);

        var decided = await (await DecideAsync(_admin, requested.DeletionRequestId!.Value, false)).ReadAsAsync<NoteDeletionRequestResponse>();

        decided.Status.Should().Be("Declined");
        var kept = (await GetProcedureAsync(_staff.Client, procedure.Id)).NoteList.Single();
        kept.Should().BeEquivalentTo(new { note.Id, Text = NoteText, DeletionRequestId = (Guid?)null, CanRequestDeletion = true });
        (await NotificationsAsync(_staff.Client)).Should().ContainSingle(n => n.Kind == "NoteDeletionDeclined");
        await (await RequestAsync(_staff.Client, procedure.Id, note.Id)).ShouldHaveStatusAsync(HttpStatusCode.Created);
    }

    [Test]
    public async Task DeleteDirectlyWhileRequested_ApprovesTheRequest()
    {
        var (procedure, note) = await ProcedureWithNoteAsync();
        var requested = await (await RequestAsync(_staff.Client, procedure.Id, note.Id)).ReadAsAsync<PatientProcedureNoteResponse>(HttpStatusCode.Created);

        await (await DeleteAsync(_admin, procedure.Id, note.Id)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        var request = await (await _admin.GetAsync(RequestUrl(requested.DeletionRequestId!.Value))).ReadAsAsync<NoteDeletionRequestResponse>();
        request.Should().BeEquivalentTo(new { Status = "Approved", DecidedByUsername = AdminUsername, Note = new { DeletionReason = AdminReason } });
        (await NotificationsAsync(_staff.Client)).Should().ContainSingle(n => n.Kind == "NoteDeletionApproved");
    }

    [Test]
    public async Task DeletingTheProcedure_ClosesItsWaitingRequests()
    {
        var (procedure, note) = await ProcedureWithNoteAsync();
        var requested = await (await RequestAsync(_staff.Client, procedure.Id, note.Id)).ReadAsAsync<PatientProcedureNoteResponse>(HttpStatusCode.Created);
        await (await PostProcedureStatusAsync(_admin, procedure.Id, "Cancelled", "Synthetic cancellation")).ShouldHaveStatusAsync(HttpStatusCode.OK);

        await (await _admin.DeleteAsync(ProcedureUrl(procedure.Id))).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        var request = await (await _admin.GetAsync(RequestUrl(requested.DeletionRequestId!.Value))).ReadAsAsync<NoteDeletionRequestResponse>();
        request.Should().BeEquivalentTo(new { Status = "Closed", DecidedByUsername = AdminUsername, Note = (object?)null, ProcedureTitle = (string?)null, CanDecide = false });
        request.Patient!.Id.Should().Be(procedure.PatientId);
        await (await DecideAsync(_admin, request.Id, true)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task TwoRequestsAtOnce_OnlyOneIsMade()
    {
        var (procedure, note) = await ProcedureWithNoteAsync();

        var responses = await Task.WhenAll(RequestAsync(_staff.Client, procedure.Id, note.Id), RequestAsync(_admin, procedure.Id, note.Id));

        responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.Created, HttpStatusCode.Conflict]);
        var pending = await RunInScopeAsync(sp => sp.GetRequiredService<MedAppDbContext>().ProcedureNoteDeletionRequests
            .CountAsync(r => r.NoteId == note.Id && r.Status == NoteDeletionRequestStatus.Pending));
        pending.Should().Be(1);
    }

    [Test]
    public async Task Anonymising_RemovesTheReasonsAndClosesWaitingRequests()
    {
        var (procedure, note) = await ProcedureWithNoteAsync();
        var requested = await (await RequestAsync(_staff.Client, procedure.Id, note.Id)).ReadAsAsync<PatientProcedureNoteResponse>(HttpStatusCode.Created);
        await (await PostProcedureStatusAsync(_admin, procedure.Id, "Cancelled", "Synthetic cancellation")).ShouldHaveStatusAsync(HttpStatusCode.OK);
        var patient = await (await _admin.GetAsync($"/api/patients/{procedure.PatientId}")).ReadAsAsync<PatientResponse>();

        await (await _admin.PostAsJsonAsync($"/api/patients/{procedure.PatientId}/anonymise", new AnonymisePatientRequest
            {
                Reason = "RetentionPeriodEnded",
                Confirmation = $"{patient.FirstName} {patient.LastName}"
            }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        var request = await (await _admin.GetAsync(RequestUrl(requested.DeletionRequestId!.Value))).ReadAsAsync<NoteDeletionRequestResponse>();
        request.Status.Should().Be("Closed");
        request.Reason.Should().NotContain("another procedure");
        (await GetProcedureAsync(_admin, procedure.Id)).NoteList.Single().DeletionRequestId.Should().BeNull();
    }
}
