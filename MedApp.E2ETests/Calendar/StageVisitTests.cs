using System.Text.RegularExpressions;
using MedApp.E2ETests.Common;
using Microsoft.Playwright;

namespace MedApp.E2ETests.Calendar;

/// <summary>
/// The visit rule: a stage that requires a visit asks for one to be booked, and a senior clinician (whose role allows
/// finishing a stage without its visit; the built-in Admin role doesn't) can finish it without one, in a browser of
/// their own, giving a reason, which the patient's timeline shows the E2E admin.
/// </summary>
[TestFixture]
public sealed class StageVisitTests : E2ETest
{
    private const string Reason = "Synthetic reason from the end-to-end tests.";
    private static readonly string ChosenPassword = E2EData.NewPassword();

    [Test]
    public async Task RequiredVisit_SeniorClinicianFinishesWithoutOne_WithAReason()
    {
        var (template, stages) = await CreateTemplateAsync("Required", "None");
        var patient = await CreatePatientAsync();
        var pathway = await CreatePathwayAsync(patient.Id, template.Id);

        // A senior clinician, with a password of their own, assigned to the pathway (so not asked whether to work on
        // someone else's).
        var username = E2EData.UniqueName("senior").Replace(' ', '-').ToLowerInvariant();
        var firstPassword = await Api.CreateUserAsync(username, "Senior clinician");
        Data.OnCleanUp($"user {username}", () => Api.DeleteUserAsync(username, E2ESettings.Password!));
        await (await SignInToApiAsync(username, firstPassword)).UpdateOwnPasswordAsync(username, firstPassword, ChosenPassword);
        await Api.AssignAsync(pathway.Id, await Api.UserIdOfAsync(username));

        // They sign in, in a browser of their own, and open the pathway.
        var options = ContextOptions();
        options.StorageStatePath = null;
        await using var theirs = await Browser.NewContextAsync(options);
        theirs.SetDefaultTimeout(15_000);
        var page = await theirs.NewPageAsync();
        await OpenAsync(page, "/login");
        await page.GetByRole(AriaRole.Textbox, new() { Name = "Username" }).FillAsync(username);
        await page.GetByRole(AriaRole.Textbox, new() { Name = "Password" }).FillAsync(ChosenPassword);
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
        await Expect(page).Not.ToHaveURLAsync(new Regex("/login"));
        await OpenAsync(page, $"/pathways/{pathway.Id}");
        var main = page.GetByRole(AriaRole.Main);

        // Its first stage needs a visit, which the app has asked to be booked.
        var visit = main.GetByRole(AriaRole.Group, new() { Name = "Visit", Exact = true });
        await Expect(visit).ToContainTextAsync("Visit required");
        await Expect(visit).ToContainTextAsync("Requires appointment");
        await Expect(visit).ToContainTextAsync("the stage can't be finished until then");
        await Expect(visit.GetByRole(AriaRole.Button, new() { Name = "Book Visit" })).ToBeVisibleAsync();

        // Finishing it anyway asks why.
        await main.GetByRole(AriaRole.Button, new() { Name = "Finish Stage" }).ClickAsync();
        var confirm = page.GetByRole(AriaRole.Dialog, new() { Name = "Finish without a visit?" });
        await Expect(confirm).ToContainTextAsync($"{stages[0].Name} requires a visit");
        await Expect(confirm).ToContainTextAsync("Its visit comes off Requires Appointment.");
        var finish = confirm.GetByRole(AriaRole.Button, new() { Name = "Finish Without Visit" });
        await Expect(finish).ToBeDisabledAsync();
        await confirm.GetByRole(AriaRole.Textbox, new() { Name = "Reason" }).FillAsync(Reason);
        await finish.ClickAsync();
        await Expect(confirm).ToBeHiddenAsync();

        // The pathway moves on to the second stage; the first is finished, and its visit is off Requires Appointment.
        await Expect(main).ToContainTextAsync($"2. {stages[1].Name}");
        await Expect(main).ToContainTextAsync("1 of 2");
        await Expect(main.GetByRole(AriaRole.Group, new() { Name = "Visit", Exact = true })).ToHaveCountAsync(0);

        var saved = await Api.FindPathwayAsync(pathway.Id);
        Assert.That(saved!.Stages[0].IsLocked, Is.True);
        var appointments = await Api.AppointmentsOfPatientAsync(patient.Id);
        Assert.That(appointments.Select(a => a.Status), Is.EqualTo(new[] { "Cancelled" }));

        // The patient's timeline shows the E2E admin it was finished without a visit, and why.
        await OpenAsync($"/patients/{patient.Id}");
        await Expect(Main).ToContainTextAsync($"Finished {stages[0].Name} without a visit");
        await Expect(Main).ToContainTextAsync(Reason);
    }
}
