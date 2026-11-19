using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Contracts.Notifications.Responses;
using MedApp.Contracts.Patients.Requests;
using MedApp.Contracts.Patients.Responses;
using MedApp.Contracts.Pathways.NoteDeletionRequests.Requests;
using MedApp.Contracts.Pathways.NoteDeletionRequests.Responses;
using MedApp.Contracts.Pathways.PatientPathways.Requests;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Contracts.Pathways.Timeline.Responses;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Infrastructure.Data;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Pathways;

/// <summary>
/// Deleting a pathway's notes through the API: admins delete one directly, with a reason; staff ask, every admin is
/// told, and one decides from the request (delete or keep), with the requester told. Deleting is soft: the note is
/// hidden from the pathway for everyone, kept in the database and listed in the patient's export as deleted. Neither
/// works on a restricted record, and only admins delete or decide.
/// </summary>
[TestFixture]
public sealed class NoteDeletionTests : IntegrationTest
{
    private const string NoteText = "Synthetic note: written on the wrong record.";
    private const string RequestDetail = "Synthetic detail: this belongs to another pathway.";
    private const string AdminDetail = "Synthetic detail: confirmed with the author.";

    private HttpClient _admin = null!;
    private StaffAccount _staff = null!;

    [SetUp]
    public async Task SignInAsync()
    {
        _admin = await SignInAsAdminAsync();
        _staff = await CreateStaffAsync(UserRole);
    }

    private static string NoteUrl(Guid pathwayId, Guid noteId) => $"{PathwayUrl(pathwayId)}/notes/{noteId}";

    private static string RequestUrl(Guid requestId) => $"/api/pathways/note-deletion-requests/{requestId}";

    private static Task<HttpResponseMessage> DeleteAsync(HttpClient client, Guid pathwayId, Guid noteId, int expectedVersion = 1) =>
        client.PostAsJsonAsync($"{NoteUrl(pathwayId, noteId)}/delete",
            new DeletePathwayNoteRequest { Reason = "WrittenInError", Detail = AdminDetail, ExpectedVersion = expectedVersion });

    private static Task<HttpResponseMessage> RequestAsync(HttpClient client, Guid pathwayId, Guid noteId) =>
        client.PostAsJsonAsync($"{NoteUrl(pathwayId, noteId)}/deletion-requests",
            new RequestNoteDeletionRequest { Reason = "WrongPatient", Detail = RequestDetail });

    private static Task<HttpResponseMessage> DecideAsync(HttpClient client, Guid requestId, bool deleteNote, int? expectedVersion = 1) =>
        client.PostAsJsonAsync($"{RequestUrl(requestId)}/decision",
            new DecideNoteDeletionRequestRequest { DeleteNote = deleteNote, ExpectedVersion = deleteNote ? expectedVersion : null });

    private static async Task<PatientPathwayResponse> GetPathwayAsync(HttpClient client, Guid pathwayId) =>
        await (await client.GetAsync(PathwayUrl(pathwayId))).ReadAsAsync<PatientPathwayResponse>();

    private static async Task<List<NotificationResponse>> NotificationsAsync(HttpClient client) =>
        (await (await client.GetAsync("/api/notifications")).ReadAsAsync<NotificationsResponse>()).Items.ToList();

    private async Task<(PatientPathwayResponse Pathway, PatientPathwayNoteResponse Note)> PathwayWithNoteAsync()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin);
        var note = await (await _staff.Client.PostAsJsonAsync($"{PathwayUrl(pathway.Id)}/notes", new AddPathwayNoteRequest { Text = NoteText }))
            .ReadAsAsync<PatientPathwayNoteResponse>(HttpStatusCode.Created);
        return (pathway, note);
    }

    [Test]
    public async Task Staff_CantDeleteReadOrDecide()
    {
        var (pathway, note) = await PathwayWithNoteAsync();
        var requested = await (await RequestAsync(_staff.Client, pathway.Id, note.Id)).ReadAsAsync<PatientPathwayNoteResponse>(HttpStatusCode.Created);

        await (await DeleteAsync(_staff.Client, pathway.Id, note.Id)).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await (await _staff.Client.GetAsync(RequestUrl(requested.DeletionRequestId!.Value))).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await (await DecideAsync(_staff.Client, requested.DeletionRequestId.Value, true)).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        (await GetPathwayAsync(_staff.Client, pathway.Id)).NoteList.Should().ContainSingle(n => n.Id == note.Id);
    }

    [Test]
    public async Task AdminDeletes_TheNoteIsHiddenForEveryoneButKeptAuditedAndExported()
    {
        var (pathway, note) = await PathwayWithNoteAsync();

        await (await DeleteAsync(_admin, pathway.Id, note.Id)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        (await GetPathwayAsync(_staff.Client, pathway.Id)).NoteList.Should().BeEmpty();
        (await GetPathwayAsync(_admin, pathway.Id)).NoteList.Should().BeEmpty();
        var (stored, audit) = await RunInScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<MedAppDbContext>();
            return (
                await db.PatientPathwayNotes.AsNoTracking().SingleAsync(n => n.Id == note.Id),
                await db.PatientPathwayAuditEntries.AsNoTracking()
                    .SingleAsync(e => e.PatientPathwayId == pathway.Id && e.Action == PatientPathwayAuditAction.NoteDeleted));
        });
        stored.Should().BeEquivalentTo(new
        {
            Text = NoteText, DeletionReason = (NoteDeletionReason?)NoteDeletionReason.WrittenInError, DeletionDetail = AdminDetail,
            PendingDeletionRequestId = (Guid?)null
        });
        stored.DeletedAtUtc.Should().NotBeNull();
        audit.Details.Should().Contain(AdminDetail).And.Contain("WrittenInError").And.NotContain(NoteText).And.Contain(note.Id.ToString());

        var export = await (await _admin.GetAsync($"/api/patients/{pathway.PatientId}/export")).ReadAsAsync<PatientExportResponse>();
        export.Pathways.Single().Notes.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            note.Id, Text = NoteText, DeletedBy = AdminUsername, DeletionReason = "WrittenInError", DeletionDetail = AdminDetail,
            IsAboutAnotherPatient = false
        });

        // The author is told their note went.
        (await NotificationsAsync(_staff.Client)).Should().ContainSingle(n => n.Kind == "AuthorsNoteDeleted")
            .Which.Message.Should().Be($"{AdminUsername} deleted a note you wrote on a pathway.");

        // Deleting it again, or editing it, is refused.
        await (await DeleteAsync(_admin, pathway.Id, note.Id)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await (await _staff.Client.PutAsJsonAsync(NoteUrl(pathway.Id, note.Id), new EditPathwayNoteRequest { Text = "Synthetic edit.", ExpectedVersion = 1 }))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Timeline_StaffSeeThatANoteWasDeleted_AdminsSeeItsTextAndTheReason()
    {
        var (pathway, note) = await PathwayWithNoteAsync();
        Clock.Advance(TimeSpan.FromMinutes(1));
        await (await DeleteAsync(_admin, pathway.Id, note.Id)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        var url = $"/api/patients/{pathway.PatientId}/timeline?Kinds=Notes";

        var staffBody = await (await _staff.Client.GetAsync(url)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        var adminPage = await (await _admin.GetAsync(url)).ReadAsAsync<PatientTimelinePageResponse>();

        var raw = await staffBody.Content.ReadAsStringAsync();
        raw.Should().Contain("NoteDeleted").And.NotContain(NoteText).And.NotContain(AdminDetail).And.NotContain("Written in error");
        var deleted = adminPage.Items.First();
        deleted.Action.Should().Be("NoteDeleted");
        deleted.Reason.Should().Be($"Written in error: {AdminDetail}");
        deleted.Changes.Single().From.Should().Be(NoteText);
    }

    [Test]
    public async Task DeleteAndDecide_NoteEditedSinceTheAdminOpenedIt_Are409AndKeepIt()
    {
        var (pathway, note) = await PathwayWithNoteAsync();
        var requested = await (await RequestAsync(_staff.Client, pathway.Id, note.Id)).ReadAsAsync<PatientPathwayNoteResponse>(HttpStatusCode.Created);
        // The author edits it after the admin has read version 1.
        await (await _staff.Client.PutAsJsonAsync(NoteUrl(pathway.Id, note.Id), new EditPathwayNoteRequest { Text = "Synthetic correction.", ExpectedVersion = 1 }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        (await (await DecideAsync(_admin, requested.DeletionRequestId!.Value, true, expectedVersion: 1)).ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict))
            .Title.Should().Contain("edited after you opened it");
        await (await DeleteAsync(_admin, pathway.Id, note.Id, expectedVersion: 1)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);

        var kept = (await GetPathwayAsync(_admin, pathway.Id)).NoteList.Single();
        kept.Text.Should().Be("Synthetic correction.");
        var request = await (await _admin.GetAsync(RequestUrl(requested.DeletionRequestId.Value))).ReadAsAsync<NoteDeletionRequestResponse>();
        request.Should().BeEquivalentTo(new { Status = "Pending", Note = new { Version = 2 } });
        request.Note!.LastEditedAtUtc.Should().BeAfter(request.RequestedAtUtc);
        await (await DecideAsync(_admin, request.Id, true, expectedVersion: 2)).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task Flags_SayWhatTheSignedInUserCanDo()
    {
        var (pathway, note) = await PathwayWithNoteAsync();

        (await GetPathwayAsync(_staff.Client, pathway.Id)).NoteList.Single().Should().BeEquivalentTo(new { CanDelete = false, CanRequestDeletion = true });
        (await GetPathwayAsync(_admin, pathway.Id)).NoteList.Single().Should().BeEquivalentTo(new { CanDelete = true, CanRequestDeletion = false });
    }

    [Test]
    public async Task DeleteAndRequest_RestrictedRecord_Are409()
    {
        var (pathway, note) = await PathwayWithNoteAsync();
        await (await _admin.PutAsJsonAsync($"/api/patients/{pathway.PatientId}/restriction",
                new SetPatientFlagRequest { Set = true, Reason = "Synthetic accuracy check", ConfirmOpenPathways = true }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        await (await DeleteAsync(_admin, pathway.Id, note.Id)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await (await RequestAsync(_staff.Client, pathway.Id, note.Id)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        var reloaded = (await GetPathwayAsync(_admin, pathway.Id)).NoteList.Single();
        reloaded.Should().BeEquivalentTo(new { CanDelete = false, CanRequestDeletion = false });
    }

    [Test]
    public async Task Decide_RestrictedAfterTheRequest_Is409AndTheDialogSaysWhy()
    {
        var (pathway, note) = await PathwayWithNoteAsync();
        var requested = await (await RequestAsync(_staff.Client, pathway.Id, note.Id)).ReadAsAsync<PatientPathwayNoteResponse>(HttpStatusCode.Created);
        await (await _admin.PutAsJsonAsync($"/api/patients/{pathway.PatientId}/restriction",
                new SetPatientFlagRequest { Set = true, Reason = "Synthetic accuracy check", ConfirmOpenPathways = true }))
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
        var (pathway, note) = await PathwayWithNoteAsync();

        // Staff ask: the note shows the request, and asking again names it.
        var requested = await (await RequestAsync(_staff.Client, pathway.Id, note.Id)).ReadAsAsync<PatientPathwayNoteResponse>(HttpStatusCode.Created);
        var requestId = requested.DeletionRequestId!.Value;
        requested.CanRequestDeletion.Should().BeFalse();
        (await GetPathwayAsync(_staff.Client, pathway.Id)).NoteList.Single().DeletionRequestId.Should().Be(requestId);
        (await (await RequestAsync(await ClinicalAsync(_admin), pathway.Id, note.Id)).ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict))
            .Title.Should().Contain(_staff.Username);

        // Every admin is told, with a link to the request; the requester isn't.
        var told = (await NotificationsAsync(_admin)).Should().ContainSingle(n => n.Kind == "NoteDeletionRequested").Subject;
        told.Should().BeEquivalentTo(new
        {
            Title = "Note deletion requested",
            Message = $"{_staff.Username} asked to delete a note on a pathway.",
            PathwayId = (Guid?)pathway.Id,
            NoteDeletionRequestId = (Guid?)requestId
        });
        (await NotificationsAsync(_staff.Client)).Should().NotContain(n => n.Kind == "NoteDeletionRequested");

        // The admin's dialog: the note, the patient and the request.
        var request = await (await _admin.GetAsync(RequestUrl(requestId))).ReadAsAsync<NoteDeletionRequestResponse>();
        request.Should().BeEquivalentTo(new
        {
            Status = "Pending",
            Reason = "WrongPatient",
            Detail = RequestDetail,
            RequestedByUsername = _staff.Username,
            PathwayId = pathway.Id,
            PathwayTitle = pathway.Title,
            Patient = new { Id = pathway.PatientId },
            Note = new { note.Id, Text = NoteText, AuthorUsername = _staff.Username, Version = 1 },
            CanDecide = true
        });

        // Delete note: hidden, the request approved, the requester told.
        var decided = await (await DecideAsync(_admin, requestId, true)).ReadAsAsync<NoteDeletionRequestResponse>();
        decided.Should().BeEquivalentTo(new
        {
            Status = "Approved",
            DecidedByUsername = AdminUsername,
            CanDecide = false,
            Note = new { DeletionReason = "WrongPatient", DeletionDetail = RequestDetail, DeletedByUsername = AdminUsername }
        });
        (await GetPathwayAsync(_staff.Client, pathway.Id)).NoteList.Should().BeEmpty();
        (await NotificationsAsync(_staff.Client)).Should().ContainSingle(n => n.Kind == "NoteDeletionApproved")
            .Which.Message.Should().Be($"{AdminUsername} deleted the note you asked to delete.");

        // Deciding it again is refused.
        (await (await DecideAsync(_admin, requestId, false)).ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict))
            .Title.Should().Contain("already deleted this note");

        var export = await (await _admin.GetAsync($"/api/patients/{pathway.PatientId}/export")).ReadAsAsync<PatientExportResponse>();
        export.NoteDeletionRequests.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Id = requestId, NoteId = note.Id, RequestedBy = _staff.Username, Reason = "WrongPatient", Detail = RequestDetail,
            Status = "Approved", DecidedBy = AdminUsername
        });
        // Deleted as on the wrong patient: flagged for whoever checks the export before it's released.
        export.Pathways.Single().Notes.Single().IsAboutAnotherPatient.Should().BeTrue();
    }

    [Test]
    public async Task RequestThenKeep_LeavesTheNoteAndItCanBeAskedForAgain()
    {
        var (pathway, note) = await PathwayWithNoteAsync();
        var requested = await (await RequestAsync(_staff.Client, pathway.Id, note.Id)).ReadAsAsync<PatientPathwayNoteResponse>(HttpStatusCode.Created);

        var decided = await (await DecideAsync(_admin, requested.DeletionRequestId!.Value, false)).ReadAsAsync<NoteDeletionRequestResponse>();

        decided.Status.Should().Be("Declined");
        var kept = (await GetPathwayAsync(_staff.Client, pathway.Id)).NoteList.Single();
        kept.Should().BeEquivalentTo(new { note.Id, Text = NoteText, DeletionRequestId = (Guid?)null, CanRequestDeletion = true });
        (await NotificationsAsync(_staff.Client)).Should().ContainSingle(n => n.Kind == "NoteDeletionDeclined");
        await (await RequestAsync(_staff.Client, pathway.Id, note.Id)).ShouldHaveStatusAsync(HttpStatusCode.Created);
    }

    [Test]
    public async Task DeleteDirectlyWhileRequested_ApprovesTheRequest()
    {
        var (pathway, note) = await PathwayWithNoteAsync();
        var requested = await (await RequestAsync(_staff.Client, pathway.Id, note.Id)).ReadAsAsync<PatientPathwayNoteResponse>(HttpStatusCode.Created);

        await (await DeleteAsync(_admin, pathway.Id, note.Id)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        var request = await (await _admin.GetAsync(RequestUrl(requested.DeletionRequestId!.Value))).ReadAsAsync<NoteDeletionRequestResponse>();
        request.Should().BeEquivalentTo(new
        {
            Status = "Approved", DecidedByUsername = AdminUsername, Note = new { DeletionReason = "WrittenInError", DeletionDetail = AdminDetail }
        });
        (await NotificationsAsync(_staff.Client)).Should().ContainSingle(n => n.Kind == "NoteDeletionApproved");
    }

    [Test]
    public async Task DeletingThePathway_ClosesItsWaitingRequests()
    {
        var (pathway, note) = await PathwayWithNoteAsync();
        var requested = await (await RequestAsync(_staff.Client, pathway.Id, note.Id)).ReadAsAsync<PatientPathwayNoteResponse>(HttpStatusCode.Created);
        await (await PostPathwayStatusAsync(_admin, pathway.Id, "Cancelled", "Synthetic cancellation")).ShouldHaveStatusAsync(HttpStatusCode.OK);

        await (await (await ClinicalAsync(_admin)).DeleteAsync(PathwayUrl(pathway.Id))).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        var request = await (await _admin.GetAsync(RequestUrl(requested.DeletionRequestId!.Value))).ReadAsAsync<NoteDeletionRequestResponse>();
        // Closed by whoever deleted the pathway (staff: admins are read only).
        request.Should().BeEquivalentTo(new { Status = "Closed", DecidedByUsername = (await ClinicStaffAsync()).Username, Note = (object?)null, PathwayTitle = (string?)null, CanDecide = false });
        request.Patient!.Id.Should().Be(pathway.PatientId);
        await (await DecideAsync(_admin, request.Id, true)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task TwoRequestsAtOnce_OnlyOneIsMade()
    {
        var (pathway, note) = await PathwayWithNoteAsync();

        var other = await ClinicalAsync(_admin);
        var responses = await Task.WhenAll(RequestAsync(_staff.Client, pathway.Id, note.Id), RequestAsync(other, pathway.Id, note.Id));

        responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.Created, HttpStatusCode.Conflict]);
        var pending = await RunInScopeAsync(sp => sp.GetRequiredService<MedAppDbContext>().PathwayNoteDeletionRequests
            .CountAsync(r => r.NoteId == note.Id && r.Status == NoteDeletionRequestStatus.Pending));
        pending.Should().Be(1);
    }

    [Test]
    public async Task Anonymising_RemovesTheReasonsAndClosesWaitingRequests()
    {
        var (pathway, note) = await PathwayWithNoteAsync();
        var requested = await (await RequestAsync(_staff.Client, pathway.Id, note.Id)).ReadAsAsync<PatientPathwayNoteResponse>(HttpStatusCode.Created);
        await (await PostPathwayStatusAsync(_admin, pathway.Id, "Cancelled", "Synthetic cancellation")).ShouldHaveStatusAsync(HttpStatusCode.OK);
        var patient = await (await _admin.GetAsync($"/api/patients/{pathway.PatientId}")).ReadAsAsync<PatientResponse>();

        await (await _admin.PostAsJsonAsync($"/api/patients/{pathway.PatientId}/anonymise", new AnonymisePatientRequest
            {
                Reason = "RetentionPeriodEnded",
                Confirmation = $"{patient.FirstName} {patient.LastName}"
            }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        var request = await (await _admin.GetAsync(RequestUrl(requested.DeletionRequestId!.Value))).ReadAsAsync<NoteDeletionRequestResponse>();
        request.Status.Should().Be("Closed");
        request.Detail.Should().NotContain("another pathway");
        (await GetPathwayAsync(_admin, pathway.Id)).NoteList.Single().DeletionRequestId.Should().BeNull();
    }
}
