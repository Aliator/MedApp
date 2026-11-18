using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Contracts.Calendar.Requests;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Contracts.Mdt.Requests;
using MedApp.Contracts.Mdt.Responses;
using MedApp.Domain.Teams;
using MedApp.IntegrationTests.Common;

namespace MedApp.IntegrationTests.Pathways;

/// <summary>
/// The MDT list end to end: an MDT arranged, a pathway listed, its outcome recorded by a clinician once the meeting has
/// started, and a cancelled MDT's items waiting to be listed again (the migration, the keys and the SQL behind them).
/// </summary>
[TestFixture]
public sealed class MdtTests : IntegrationTest
{
    private static async Task<MeetingResponse> ArrangeMdtAsync(HttpClient admin, Guid attendee, DateTime startUtc)
    {
        var response = await admin.PostAsJsonAsync("/api/calendar/meetings", new MeetingRequest
        {
            Title = UniqueName("MDT"),
            StartUtc = startUtc,
            EndUtc = startUtc.AddHours(1),
            AttendeeUserIds = [attendee],
            IsMdt = true,
            TeamId = Team.GeneralId,
        });
        return await response.ReadAsAsync<MeetingResponse>(HttpStatusCode.Created);
    }

    [Test]
    public async Task ListThenRecordOutcome_TheClinicianWritesIt_AndAnMdtWithOutcomesCantBeCancelled()
    {
        var admin = await SignInAsAdminAsync();
        var clinician = await CreateStaffAsync("Clinician");
        var pathway = await CreatePatientWithPathwayAsync(admin);
        var start = NextBookableStart();
        var mdt = await ArrangeMdtAsync(admin, clinician.UserId, start);
        mdt.IsMdt.Should().BeTrue();

        var listed = await (await admin.PostAsJsonAsync("/api/mdt/items", new ListForMdtRequest
        {
            PathwayId = pathway.Id, MeetingId = mdt.Id, Question = "Synthetic question?",
        })).ReadAsAsync<MdtItemResponse>(HttpStatusCode.Created);
        (await admin.PostAsJsonAsync("/api/mdt/items", new ListForMdtRequest
        {
            PathwayId = pathway.Id, MeetingId = mdt.Id, Question = "Again?",
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var list = await (await admin.GetAsync($"/api/mdt/meetings/{mdt.Id}/items")).ReadAsAsync<MdtMeetingListResponse>();
        list.Items.Should().ContainSingle().Which.Question.Should().Be("Synthetic question?");

        // Admin doesn't record outcomes (a clinical decision); the clinician does, once the meeting has started.
        var outcome = new RecordMdtOutcomeRequest { Outcome = "Synthetic outcome.", PresentUserIds = [clinician.UserId], ExpectedVersion = listed.Version };
        (await admin.PostAsJsonAsync($"/api/mdt/items/{listed.Id}/outcome", outcome)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await clinician.Client.PostAsJsonAsync($"/api/mdt/items/{listed.Id}/outcome", outcome)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        MoveClockPast(start);
        var discussed = await (await clinician.Client.PostAsJsonAsync($"/api/mdt/items/{listed.Id}/outcome", outcome))
            .ReadAsAsync<MdtItemResponse>();
        discussed.Status.Should().Be("Discussed");
        discussed.Present.Should().Equal(clinician.Username);

        (await admin.DeleteAsync($"/api/calendar/meetings/{mdt.Id}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var items = await (await admin.GetAsync($"/api/mdt/pathways/{pathway.Id}/items")).ReadAsAsync<List<MdtItemResponse>>();
        items.Should().ContainSingle().Which.Outcome.Should().Be("Synthetic outcome.");
    }

    [Test]
    public async Task CancellingAnMdt_KeepsItsItemsListed_ToListAgain()
    {
        var admin = await SignInAsAdminAsync();
        var clinician = await CreateStaffAsync("Clinician");
        var pathway = await CreatePatientWithPathwayAsync(admin);
        var mdt = await ArrangeMdtAsync(admin, clinician.UserId, UkAt(NextWorkingDay(), 9));
        var later = await ArrangeMdtAsync(admin, clinician.UserId, UkAt(NextWorkingDay(3), 9));
        var listed = await (await admin.PostAsJsonAsync("/api/mdt/items", new ListForMdtRequest
        {
            PathwayId = pathway.Id, MeetingId = mdt.Id, Question = "Synthetic question?",
        })).ReadAsAsync<MdtItemResponse>(HttpStatusCode.Created);

        (await admin.DeleteAsync($"/api/calendar/meetings/{mdt.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var waiting = await (await admin.GetAsync("/api/mdt/awaiting-meeting")).ReadAsAsync<List<MdtItemResponse>>();
        var item = waiting.Should().ContainSingle().Subject;
        item.MeetingId.Should().BeNull();
        item.Status.Should().Be("Listed");
        item.MeetingCancelledStartUtc.Should().NotBeNull();

        var moved = await (await admin.PostAsJsonAsync($"/api/mdt/items/{listed.Id}/defer", new DeferMdtItemRequest
        {
            MeetingId = later.Id, ExpectedVersion = item.Version,
        })).ReadAsAsync<MdtItemResponse>();
        moved.MeetingId.Should().Be(later.Id);
        moved.Status.Should().Be("Listed");
    }
}
