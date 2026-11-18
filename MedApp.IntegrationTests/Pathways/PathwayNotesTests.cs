using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.PatientPathways.Commands.UpdatePatientPathway;
using MedApp.Contracts.Patients.Requests;
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
/// A pathway's notes through the API: added by staff, edited only by their author from the version they saw, never
/// while the pathway is closed or the record restricted, with every version in its own table and the audit (and so
/// the staff timeline) naming the note, never its text.
/// </summary>
[TestFixture]
public sealed class PathwayNotesTests : IntegrationTest
{
    private const string FirstText = "Synthetic note: prefers a **morning** appointment.";
    private const string EditedText = "Synthetic note: prefers an afternoon appointment.";

    private HttpClient _admin = null!;
    private StaffAccount _staff = null!;

    [SetUp]
    public async Task SignInAsync()
    {
        _admin = await SignInAsAdminAsync();
        _staff = await CreateStaffAsync(UserRole);
    }

    private static string NotesUrl(Guid pathwayId) => $"{PathwayUrl(pathwayId)}/notes";

    private static Task<HttpResponseMessage> AddAsync(HttpClient client, Guid pathwayId, string text) =>
        client.PostAsJsonAsync(NotesUrl(pathwayId), new AddPathwayNoteRequest { Text = text });

    private static Task<HttpResponseMessage> EditAsync(HttpClient client, Guid pathwayId, Guid noteId, string text, int expectedVersion) =>
        client.PutAsJsonAsync($"{NotesUrl(pathwayId)}/{noteId}", new EditPathwayNoteRequest { Text = text, ExpectedVersion = expectedVersion });

    private static async Task<PatientPathwayResponse> GetAsync(HttpClient client, Guid pathwayId) =>
        await (await client.GetAsync(PathwayUrl(pathwayId))).ReadAsAsync<PatientPathwayResponse>();

    [Test]
    public async Task Add_ByStaff_Is201AndShowsOnThePathwayNewestFirst()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin);

        var first = await (await AddAsync(_staff.Client, pathway.Id, FirstText)).ReadAsAsync<PatientPathwayNoteResponse>(HttpStatusCode.Created);
        Clock.Advance(TimeSpan.FromMinutes(5));
        var second = await (await AddAsync(_admin, pathway.Id, "Synthetic second note.")).ReadAsAsync<PatientPathwayNoteResponse>(HttpStatusCode.Created);

        first.Should().BeEquivalentTo(new { AuthorUserId = (Guid?)_staff.UserId, AuthorUsername = _staff.Username, Text = FirstText, Version = 1, CanEdit = true });
        first.LastEditedAtUtc.Should().BeNull();
        var asStaff = await GetAsync(_staff.Client, pathway.Id);
        asStaff.CanAddNotes.Should().BeTrue();
        asStaff.NoteList.Select(n => (n.Id, n.CanEdit)).Should().Equal((second.Id, false), (first.Id, true));
        asStaff.NoteList[0].AuthorUsername.Should().Be(AdminUsername);
        (await GetAsync(_admin, pathway.Id)).NoteList.Select(n => n.CanEdit).Should().Equal(true, false);
    }

    [Test]
    public async Task Add_WritesVersionOneAndAnAuditEntryWithoutTheText()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin);

        var note = await (await AddAsync(_staff.Client, pathway.Id, FirstText)).ReadAsAsync<PatientPathwayNoteResponse>(HttpStatusCode.Created);

        var (versions, audit) = await RunInScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<MedAppDbContext>();
            return (
                await db.PatientPathwayNoteVersions.AsNoTracking().Where(v => v.NoteId == note.Id).ToListAsync(),
                await db.PatientPathwayAuditEntries.AsNoTracking()
                    .Where(e => e.PatientPathwayId == pathway.Id && e.Action == PatientPathwayAuditAction.NoteAdded).ToListAsync());
        });
        versions.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Version = 1, Text = FirstText, PatientPathwayId = pathway.Id, pathway.PatientId, UserId = (Guid?)_staff.UserId, _staff.Username
        });
        audit.Should().ContainSingle().Which.Details.Should().NotContain("morning").And.Contain(note.Id.ToString());
    }

    [Test]
    public async Task Create_WithNotes_MakesThemTheFirstNote()
    {
        var patient = await CreatePatientAsync(_staff.Client);
        var template = await CreateTemplateWithStagesAsync(_admin, 1);

        var pathway = await (await _staff.Client.PostAsJsonAsync("/api/pathways/patient-pathways", new CreatePatientPathwayRequest
            {
                PatientId = patient.Id,
                TemplateId = template.Id,
                Notes = FirstText,
                DueDate = UkToday.AddDays(7),
                Priority = "Medium",
            }))
            .ReadAsAsync<PatientPathwayResponse>(HttpStatusCode.Created);

        pathway.NoteList.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Text = FirstText, AuthorUsername = _staff.Username, CanEdit = true });
    }

    [Test]
    public async Task Edit_OwnNote_SavesTheNextVersion()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin);
        var note = await (await AddAsync(_staff.Client, pathway.Id, FirstText)).ReadAsAsync<PatientPathwayNoteResponse>(HttpStatusCode.Created);
        Clock.Advance(TimeSpan.FromMinutes(10));

        var edited = await (await EditAsync(_staff.Client, pathway.Id, note.Id, EditedText, 1)).ReadAsAsync<PatientPathwayNoteResponse>();

        edited.Should().BeEquivalentTo(new { note.Id, Text = EditedText, Version = 2, note.CreatedAtUtc, CanEdit = true });
        edited.LastEditedAtUtc.Should().BeCloseTo(Now, TimeSpan.FromSeconds(1));
        var versions = await RunInScopeAsync(sp => sp.GetRequiredService<MedAppDbContext>().PatientPathwayNoteVersions
            .AsNoTracking().Where(v => v.NoteId == note.Id).OrderBy(v => v.Version).Select(v => v.Text).ToListAsync());
        versions.Should().Equal(FirstText, EditedText);
    }

    [Test]
    public async Task Edit_SomeoneElsesNote_Is403ForStaffAndAdmins()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin);
        var staffNote = await (await AddAsync(_staff.Client, pathway.Id, FirstText)).ReadAsAsync<PatientPathwayNoteResponse>(HttpStatusCode.Created);
        var adminNote = await (await AddAsync(_admin, pathway.Id, FirstText)).ReadAsAsync<PatientPathwayNoteResponse>(HttpStatusCode.Created);

        (await (await EditAsync(_staff.Client, pathway.Id, adminNote.Id, EditedText, 1)).ReadAsAsync<ProblemDetails>(HttpStatusCode.Forbidden))
            .Title.Should().Be(PathwayNotes.OnlyTheAuthorMessage);
        await (await EditAsync(_admin, pathway.Id, staffNote.Id, EditedText, 1)).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);

        (await GetAsync(_admin, pathway.Id)).NoteList.Should().AllSatisfy(n => n.Text.Should().Be(FirstText));
    }

    [Test]
    public async Task Edit_FromAStaleVersion_Is409AndChangesNothing()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin);
        var note = await (await AddAsync(_staff.Client, pathway.Id, FirstText)).ReadAsAsync<PatientPathwayNoteResponse>(HttpStatusCode.Created);
        await (await EditAsync(_staff.Client, pathway.Id, note.Id, EditedText, 1)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        // Another tab still showing version 1.
        var stale = await (await EditAsync(_staff.Client, pathway.Id, note.Id, "Synthetic stale edit.", 1)).ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict);

        stale.Title.Should().Be(PathwayNotes.ChangedMeanwhileMessage);
        (await GetAsync(_staff.Client, pathway.Id)).NoteList.Single().Should().BeEquivalentTo(new { Text = EditedText, Version = 2 });
    }

    [Test]
    public async Task Add_Is201WithThePathwayAsItsLocation()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin);

        var response = await AddAsync(_staff.Client, pathway.Id, FirstText);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.OriginalString.Should().Be(PathwayUrl(pathway.Id));
        (await _staff.Client.GetAsync(response.Headers.Location)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Get_KeepsTheOldNotesStringBesideTheList_ForPagesLoadedBeforeTheChange()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin);
        await (await AddAsync(_staff.Client, pathway.Id, FirstText)).ShouldHaveStatusAsync(HttpStatusCode.Created);
        Clock.Advance(TimeSpan.FromMinutes(5));
        await (await AddAsync(_admin, pathway.Id, EditedText)).ShouldHaveStatusAsync(HttpStatusCode.Created);

        var json = await (await _staff.Client.GetAsync(PathwayUrl(pathway.Id))).Content.ReadAsStringAsync();
        var read = await GetAsync(_staff.Client, pathway.Id);

        json.Should().Contain("\"notes\":\"").And.Contain("\"noteList\":[");
        read.Notes.Should().Be(FirstText, "it's the first note's text, as a string");
        read.NoteList.Should().HaveCount(2);
    }

    [Test]
    public async Task Update_StillSendingTheOldNotesField_Is400AndChangesNothing()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin);

        var refused = await (await _staff.Client.PatchAsJsonAsync(PathwayUrl(pathway.Id),
                new UpdatePatientPathwayRequest { Title = "Synthetic renamed", Notes = "Synthetic notes from an old page." }))
            .ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest);

        refused.Errors.Should().ContainKey("Notes").WhoseValue.Should().Equal(UpdatePatientPathwayValidator.NotesMovedMessage);
        var reloaded = await GetAsync(_staff.Client, pathway.Id);
        reloaded.Title.Should().Be(pathway.Title);
        reloaded.NoteList.Should().BeEmpty();
    }

    [Test]
    public async Task Add_BlankOrTooLong_Is400()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin);

        (await (await AddAsync(_staff.Client, pathway.Id, "   ")).ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest))
            .Errors.Should().ContainKey("Text");
        await (await AddAsync(_staff.Client, pathway.Id, new string('a', PatientPathwayNote.MaxTextLength + 1)))
            .ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Add_CompletedPathway_IsAllowed_ButNotOnceCancelled()
    {
        var completed = await CreatePatientWithPathwayAsync(_admin, stages: 1);
        await FinishStageAsync(_admin, completed.Id, completed.Stages[0].Id);
        await (await PostPathwayStatusAsync(_admin, completed.Id, "Completed")).ShouldHaveStatusAsync(HttpStatusCode.OK);
        var cancelled = await CreatePatientWithPathwayAsync(_admin);
        await (await PostPathwayStatusAsync(_admin, cancelled.Id, "Cancelled", "Synthetic reason")).ShouldHaveStatusAsync(HttpStatusCode.OK);

        await (await AddAsync(_staff.Client, completed.Id, FirstText)).ShouldHaveStatusAsync(HttpStatusCode.Created);
        (await (await AddAsync(_staff.Client, cancelled.Id, FirstText)).ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict))
            .Title.Should().Contain("cancelled");
        (await GetAsync(_staff.Client, cancelled.Id)).CanAddNotes.Should().BeFalse();
    }

    [Test]
    public async Task AddAndEdit_RestrictedRecord_Are409()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin);
        var note = await (await AddAsync(_staff.Client, pathway.Id, FirstText)).ReadAsAsync<PatientPathwayNoteResponse>(HttpStatusCode.Created);
        await (await _admin.PutAsJsonAsync($"/api/patients/{pathway.PatientId}/restriction",
                new SetPatientFlagRequest { Set = true, Reason = "Synthetic accuracy check", ConfirmOpenPathways = true }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        await (await AddAsync(_staff.Client, pathway.Id, FirstText)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await (await EditAsync(_staff.Client, pathway.Id, note.Id, EditedText, 1)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        var reloaded = await GetAsync(_staff.Client, pathway.Id);
        reloaded.CanAddNotes.Should().BeFalse();
        reloaded.NoteList.Single().CanEdit.Should().BeFalse();
    }

    [Test]
    public async Task Timeline_StaffSeeThatANoteWasAddedAndEdited_AdminsSeeTheText()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin);
        var note = await (await AddAsync(_staff.Client, pathway.Id, FirstText)).ReadAsAsync<PatientPathwayNoteResponse>(HttpStatusCode.Created);
        Clock.Advance(TimeSpan.FromMinutes(1));
        await (await EditAsync(_staff.Client, pathway.Id, note.Id, EditedText, 1)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        var url = $"/api/patients/{pathway.PatientId}/timeline?Kinds=Notes";

        var staffBody = await (await _staff.Client.GetAsync(url)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        var staffPage = (await staffBody.Content.ReadFromJsonAsync<PatientTimelinePageResponse>())!;
        var adminPage = await (await _admin.GetAsync(url)).ReadAsAsync<PatientTimelinePageResponse>();

        staffPage.Items.Select(i => (i.Action, i.Username, i.Kind)).Should().Equal(
            ("NoteEdited", _staff.Username, "Notes"), ("NoteAdded", _staff.Username, "Notes"));
        var raw = await staffBody.Content.ReadAsStringAsync();
        raw.Should().NotContain("morning").And.NotContain("afternoon");
        adminPage.Items.Select(i => (i.Changes.Single().From, i.Changes.Single().To)).Should().Equal(
            (FirstText, EditedText), (null, FirstText));
    }
}
