using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Notepads.Common;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Notepads.Requests;
using MedApp.Contracts.Notepads.Responses;
using MedApp.Infrastructure.Data;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Notepads;

/// <summary>
/// Each account's notepad through the API: its own only, saved from the version last seen (another tab's save first is
/// a 409), cleared after 30 days without a change, gone with the account, and never in the activity log.
/// </summary>
[TestFixture]
public sealed class NotepadTests : IntegrationTest
{
    private const string Url = "/api/me/notepad";
    private const string Jotting = "Synthetic jotting: call the ward back after **2pm**.";

    private static Task<HttpResponseMessage> SaveAsync(HttpClient client, string text, int expectedVersion) =>
        client.PutAsJsonAsync(Url, new SaveNotepadRequest { Text = text, ExpectedVersion = expectedVersion });

    [Test]
    public async Task Get_NoneYet_IsEmptyAtVersionZero()
    {
        var staff = await CreateStaffAsync(UserRole);

        var notepad = await (await staff.Client.GetAsync(Url)).ReadAsAsync<NotepadResponse>();

        notepad.Should().BeEquivalentTo(new { Text = string.Empty, LastUpdatedUtc = (DateTime?)null, Version = 0, KeepDays = 30 });
    }

    [Test]
    public async Task Save_ThenGet_IsYoursAlone()
    {
        var staff = await CreateStaffAsync(UserRole);
        var other = await CreateStaffAsync(UserRole);

        var saved = await (await SaveAsync(staff.Client, Jotting, 0)).ReadAsAsync<NotepadResponse>();
        var again = await (await SaveAsync(staff.Client, Jotting + " Done.", saved.Version)).ReadAsAsync<NotepadResponse>();

        saved.Version.Should().Be(1);
        again.Version.Should().Be(2);
        (await (await staff.Client.GetAsync(Url)).ReadAsAsync<NotepadResponse>()).Text.Should().Be(Jotting + " Done.");
        (await (await other.Client.GetAsync(Url)).ReadAsAsync<NotepadResponse>()).Text.Should().BeEmpty();
    }

    [Test]
    public async Task Save_FromAStaleVersion_Is409AndKeepsTheOtherTabsText()
    {
        var staff = await CreateStaffAsync(UserRole);
        await (await SaveAsync(staff.Client, Jotting, 0)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        // Another tab saves version 2...
        await (await SaveAsync(staff.Client, "Synthetic: from the other tab.", 1)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        // ...then this one, still at version 1.
        var stale = await (await SaveAsync(staff.Client, "Synthetic: from this tab.", 1)).ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict);
        // And two first saves at once: the second finds one already there.
        var first = await CreateStaffAsync(UserRole);
        await (await SaveAsync(first.Client, Jotting, 0)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await SaveAsync(first.Client, Jotting, 0)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);

        stale.Title.Should().Be(NotepadRules.ChangedMeanwhileMessage);
        (await (await staff.Client.GetAsync(Url)).ReadAsAsync<NotepadResponse>()).Text.Should().Be("Synthetic: from the other tab.");
    }

    [Test]
    public async Task Save_TooLong_Is400()
    {
        var staff = await CreateStaffAsync(UserRole);

        (await (await SaveAsync(staff.Client, new string('a', 20_001), 0)).ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest))
            .Errors.Should().ContainKey("Text");
    }

    [Test]
    public async Task After30DaysWithoutAChange_ItsClearedWhenNextReadOrSaved()
    {
        var reader = await CreateStaffAsync(UserRole);
        var writer = await CreateStaffAsync(UserRole);
        await (await SaveAsync(reader.Client, Jotting, 0)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        var saved = await (await SaveAsync(writer.Client, Jotting, 0)).ReadAsAsync<NotepadResponse>();

        // A day short of 30: still there.
        Clock.Advance(TimeSpan.FromDays(29));
        (await (await reader.Client.GetAsync(Url)).ReadAsAsync<NotepadResponse>()).Text.Should().Be(Jotting);

        Clock.Advance(TimeSpan.FromDays(1));
        var read = await (await reader.Client.GetAsync(Url)).ReadAsAsync<NotepadResponse>();
        var refused = await (await SaveAsync(writer.Client, Jotting + " More.", saved.Version)).ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict);

        read.Should().BeEquivalentTo(new { Text = string.Empty, Version = 0 });
        refused.Title.Should().Be(NotepadRules.ClearedMessage(30));
        var rows = await RunInScopeAsync(sp => sp.GetRequiredService<MedAppDbContext>().Notepads.CountAsync());
        rows.Should().Be(0, "both were deleted, not just hidden");
        // Starting again works.
        await (await SaveAsync(writer.Client, "Synthetic: a fresh start.", 0)).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task DeletingTheAccount_DeletesItsNotepad()
    {
        var admin = await SignInAsAdminAsync();
        var staff = await CreateStaffAsync(UserRole);
        await (await SaveAsync(staff.Client, Jotting, 0)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        await (await admin.DeleteAsJsonAsync($"/api/auth/users/{staff.Username}", new DeleteUserRequest(AdminPassword)))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        (await RunInScopeAsync(sp => sp.GetRequiredService<MedAppDbContext>().Notepads.AnyAsync(n => n.UserId == staff.UserId)))
            .Should().BeFalse();
    }

    [Test]
    public async Task ReadingAndSaving_RecordNothingInTheActivityLog()
    {
        var staff = await CreateStaffAsync(UserRole);
        var before = await ActivityCountAsync(staff.UserId);

        await (await SaveAsync(staff.Client, Jotting, 0)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await staff.Client.GetAsync(Url)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        (await ActivityCountAsync(staff.UserId)).Should().Be(before);
    }

    [Test]
    public async Task SignedOut_Is401()
    {
        (await NewClient().GetAsync(Url)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static Task<int> ActivityCountAsync(Guid userId) =>
        RunInScopeAsync(sp => sp.GetRequiredService<MedAppDbContext>().ActivityEntries.CountAsync(e => e.UserId == userId));
}
