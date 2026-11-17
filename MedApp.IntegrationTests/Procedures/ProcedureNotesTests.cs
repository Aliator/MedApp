using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Patients.Requests;
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
/// A procedure's notes through the API: added by staff, edited only by their author from the version they saw, never
/// while the procedure is closed or the record restricted, with every version in its own table and the audit (and so
/// the staff timeline) naming the note, never its text.
/// </summary>
[TestFixture]
public sealed class ProcedureNotesTests : IntegrationTest
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

    private static string NotesUrl(Guid procedureId) => $"{ProcedureUrl(procedureId)}/notes";

    private static Task<HttpResponseMessage> AddAsync(HttpClient client, Guid procedureId, string text) =>
        client.PostAsJsonAsync(NotesUrl(procedureId), new AddProcedureNoteRequest { Text = text });

    private static Task<HttpResponseMessage> EditAsync(HttpClient client, Guid procedureId, Guid noteId, string text, int expectedVersion) =>
        client.PutAsJsonAsync($"{NotesUrl(procedureId)}/{noteId}", new EditProcedureNoteRequest { Text = text, ExpectedVersion = expectedVersion });

    private static async Task<PatientProcedureResponse> GetAsync(HttpClient client, Guid procedureId) =>
        await (await client.GetAsync(ProcedureUrl(procedureId))).ReadAsAsync<PatientProcedureResponse>();

    [Test]
    public async Task Add_ByStaff_Is201AndShowsOnTheProcedureNewestFirst()
    {
        var procedure = await CreatePatientWithProcedureAsync(_admin);

        var first = await (await AddAsync(_staff.Client, procedure.Id, FirstText)).ReadAsAsync<PatientProcedureNoteResponse>(HttpStatusCode.Created);
        Clock.Advance(TimeSpan.FromMinutes(5));
        var second = await (await AddAsync(_admin, procedure.Id, "Synthetic second note.")).ReadAsAsync<PatientProcedureNoteResponse>(HttpStatusCode.Created);

        first.Should().BeEquivalentTo(new { AuthorUserId = (Guid?)_staff.UserId, AuthorUsername = _staff.Username, Text = FirstText, Version = 1, CanEdit = true });
        first.LastEditedAtUtc.Should().BeNull();
        var asStaff = await GetAsync(_staff.Client, procedure.Id);
        asStaff.CanAddNotes.Should().BeTrue();
        asStaff.Notes.Select(n => (n.Id, n.CanEdit)).Should().Equal((second.Id, false), (first.Id, true));
        asStaff.Notes[0].AuthorUsername.Should().Be(AdminUsername);
        (await GetAsync(_admin, procedure.Id)).Notes.Select(n => n.CanEdit).Should().Equal(true, false);
    }

    [Test]
    public async Task Add_WritesVersionOneAndAnAuditEntryWithoutTheText()
    {
        var procedure = await CreatePatientWithProcedureAsync(_admin);

        var note = await (await AddAsync(_staff.Client, procedure.Id, FirstText)).ReadAsAsync<PatientProcedureNoteResponse>(HttpStatusCode.Created);

        var (versions, audit) = await RunInScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<MedAppDbContext>();
            return (
                await db.PatientProcedureNoteVersions.AsNoTracking().Where(v => v.NoteId == note.Id).ToListAsync(),
                await db.PatientProcedureAuditEntries.AsNoTracking()
                    .Where(e => e.PatientProcedureId == procedure.Id && e.Action == PatientProcedureAuditAction.NoteAdded).ToListAsync());
        });
        versions.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Version = 1, Text = FirstText, PatientProcedureId = procedure.Id, procedure.PatientId, UserId = (Guid?)_staff.UserId, _staff.Username
        });
        audit.Should().ContainSingle().Which.Details.Should().NotContain("morning").And.Contain(note.Id.ToString());
    }

    [Test]
    public async Task Create_WithNotes_MakesThemTheFirstNote()
    {
        var patient = await CreatePatientAsync(_staff.Client);
        var template = await CreateTemplateWithStagesAsync(_admin, 1);

        var procedure = await (await _staff.Client.PostAsJsonAsync("/api/procedures/patient-procedures", new CreatePatientProcedureRequest
            {
                PatientId = patient.Id,
                TemplateId = template.Id,
                Notes = FirstText,
                DueDate = UkToday.AddDays(7),
                Priority = "Medium",
            }))
            .ReadAsAsync<PatientProcedureResponse>(HttpStatusCode.Created);

        procedure.Notes.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Text = FirstText, AuthorUsername = _staff.Username, CanEdit = true });
    }

    [Test]
    public async Task Edit_OwnNote_SavesTheNextVersion()
    {
        var procedure = await CreatePatientWithProcedureAsync(_admin);
        var note = await (await AddAsync(_staff.Client, procedure.Id, FirstText)).ReadAsAsync<PatientProcedureNoteResponse>(HttpStatusCode.Created);
        Clock.Advance(TimeSpan.FromMinutes(10));

        var edited = await (await EditAsync(_staff.Client, procedure.Id, note.Id, EditedText, 1)).ReadAsAsync<PatientProcedureNoteResponse>();

        edited.Should().BeEquivalentTo(new { note.Id, Text = EditedText, Version = 2, note.CreatedAtUtc, CanEdit = true });
        edited.LastEditedAtUtc.Should().BeCloseTo(Now, TimeSpan.FromSeconds(1));
        var versions = await RunInScopeAsync(sp => sp.GetRequiredService<MedAppDbContext>().PatientProcedureNoteVersions
            .AsNoTracking().Where(v => v.NoteId == note.Id).OrderBy(v => v.Version).Select(v => v.Text).ToListAsync());
        versions.Should().Equal(FirstText, EditedText);
    }

    [Test]
    public async Task Edit_SomeoneElsesNote_Is403ForStaffAndAdmins()
    {
        var procedure = await CreatePatientWithProcedureAsync(_admin);
        var staffNote = await (await AddAsync(_staff.Client, procedure.Id, FirstText)).ReadAsAsync<PatientProcedureNoteResponse>(HttpStatusCode.Created);
        var adminNote = await (await AddAsync(_admin, procedure.Id, FirstText)).ReadAsAsync<PatientProcedureNoteResponse>(HttpStatusCode.Created);

        (await (await EditAsync(_staff.Client, procedure.Id, adminNote.Id, EditedText, 1)).ReadAsAsync<ProblemDetails>(HttpStatusCode.Forbidden))
            .Title.Should().Be(ProcedureNotes.OnlyTheAuthorMessage);
        await (await EditAsync(_admin, procedure.Id, staffNote.Id, EditedText, 1)).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);

        (await GetAsync(_admin, procedure.Id)).Notes.Should().AllSatisfy(n => n.Text.Should().Be(FirstText));
    }

    [Test]
    public async Task Edit_FromAStaleVersion_Is409AndChangesNothing()
    {
        var procedure = await CreatePatientWithProcedureAsync(_admin);
        var note = await (await AddAsync(_staff.Client, procedure.Id, FirstText)).ReadAsAsync<PatientProcedureNoteResponse>(HttpStatusCode.Created);
        await (await EditAsync(_staff.Client, procedure.Id, note.Id, EditedText, 1)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        // Another tab still showing version 1.
        var stale = await (await EditAsync(_staff.Client, procedure.Id, note.Id, "Synthetic stale edit.", 1)).ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict);

        stale.Title.Should().Be(ProcedureNotes.ChangedMeanwhileMessage);
        (await GetAsync(_staff.Client, procedure.Id)).Notes.Single().Should().BeEquivalentTo(new { Text = EditedText, Version = 2 });
    }

    [Test]
    public async Task Add_BlankOrTooLong_Is400()
    {
        var procedure = await CreatePatientWithProcedureAsync(_admin);

        (await (await AddAsync(_staff.Client, procedure.Id, "   ")).ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest))
            .Errors.Should().ContainKey("Text");
        await (await AddAsync(_staff.Client, procedure.Id, new string('a', PatientProcedureNote.MaxTextLength + 1)))
            .ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Add_CompletedProcedure_IsAllowed_ButNotOnceCancelled()
    {
        var completed = await CreatePatientWithProcedureAsync(_admin, stages: 1);
        await FinishStageAsync(_admin, completed.Id, completed.Stages[0].Id);
        await (await PostProcedureStatusAsync(_admin, completed.Id, "Completed")).ShouldHaveStatusAsync(HttpStatusCode.OK);
        var cancelled = await CreatePatientWithProcedureAsync(_admin);
        await (await PostProcedureStatusAsync(_admin, cancelled.Id, "Cancelled", "Synthetic reason")).ShouldHaveStatusAsync(HttpStatusCode.OK);

        await (await AddAsync(_staff.Client, completed.Id, FirstText)).ShouldHaveStatusAsync(HttpStatusCode.Created);
        (await (await AddAsync(_staff.Client, cancelled.Id, FirstText)).ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict))
            .Title.Should().Contain("cancelled");
        (await GetAsync(_staff.Client, cancelled.Id)).CanAddNotes.Should().BeFalse();
    }

    [Test]
    public async Task AddAndEdit_RestrictedRecord_Are409()
    {
        var procedure = await CreatePatientWithProcedureAsync(_admin);
        var note = await (await AddAsync(_staff.Client, procedure.Id, FirstText)).ReadAsAsync<PatientProcedureNoteResponse>(HttpStatusCode.Created);
        await (await _admin.PutAsJsonAsync($"/api/patients/{procedure.PatientId}/restriction",
                new SetPatientFlagRequest { Set = true, Reason = "Synthetic accuracy check", ConfirmOpenProcedures = true }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        await (await AddAsync(_staff.Client, procedure.Id, FirstText)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await (await EditAsync(_staff.Client, procedure.Id, note.Id, EditedText, 1)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        var reloaded = await GetAsync(_staff.Client, procedure.Id);
        reloaded.CanAddNotes.Should().BeFalse();
        reloaded.Notes.Single().CanEdit.Should().BeFalse();
    }

    [Test]
    public async Task Timeline_StaffSeeThatANoteWasAddedAndEdited_AdminsSeeTheText()
    {
        var procedure = await CreatePatientWithProcedureAsync(_admin);
        var note = await (await AddAsync(_staff.Client, procedure.Id, FirstText)).ReadAsAsync<PatientProcedureNoteResponse>(HttpStatusCode.Created);
        Clock.Advance(TimeSpan.FromMinutes(1));
        await (await EditAsync(_staff.Client, procedure.Id, note.Id, EditedText, 1)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        var url = $"/api/patients/{procedure.PatientId}/timeline?Kinds=Notes";

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
