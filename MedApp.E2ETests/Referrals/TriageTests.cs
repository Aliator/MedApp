using System.Text.RegularExpressions;
using MedApp.E2ETests.Common;
using Microsoft.Playwright;

namespace MedApp.E2ETests.Referrals;

/// <summary>
/// Triage: a referral registered by the E2E admin is accepted by a Clinician of its specialty, in a browser of their
/// own, choosing the template, priority and target date (nothing is prefilled); the pathway opens, linked back to the
/// referral, and whoever registered it is told.
/// </summary>
[TestFixture]
public sealed class TriageTests : E2ETest
{
    private static readonly string ChosenPassword = E2EData.NewPassword();

    [Test]
    public async Task Accept_AsAClinicianOfItsSpecialty_CreatesThePathwayLinkedToTheReferral()
    {
        var (template, _) = await CreateTemplateAsync(2);
        var specialty = await Api.FirstSpecialtyAsync();

        // A patient with a referral can't be deleted: they stay, with the referral (README); their pathway goes.
        var patient = await Api.CreatePatientAsync("Grace", E2EData.UniqueName("Patient"), new DateOnly(1984, 3, 21));
        Data.OnCleanUp($"the pathways of {patient.LastName}", async () =>
        {
            foreach (var pathway in await Api.PathwaysOfPatientAsync(patient.Id))
                await Api.CancelAndDeletePathwayAsync(pathway.Id);
        });
        var referral = await Api.RegisterReferralAsync(patient.Id, specialty.Id, "Synthetic reason for referral from the end-to-end tests.");

        // A Clinician who belongs to the specialty, with a password of their own.
        var username = E2EData.UniqueName("clinician").Replace(' ', '-').ToLowerInvariant();
        var firstPassword = await Api.CreateUserAsync(username, "Clinician");
        Data.OnCleanUp($"user {username}", () => Api.DeleteUserAsync(username, E2ESettings.Password!));
        await Api.SetSpecialtiesAsync(username, specialty.Id);
        await (await SignInToApiAsync(username, firstPassword)).UpdateOwnPasswordAsync(username, firstPassword, ChosenPassword);

        // They sign in, in a browser of their own, and open the referral.
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
        await OpenAsync(page, $"/referrals/{referral.Id}");
        var main = page.GetByRole(AriaRole.Main);

        // Accept: the template, priority and target date are chosen, never prefilled.
        await main.GetByRole(AriaRole.Button, new() { Name = "Accept" }).ClickAsync();
        var dialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Accept Referral" });
        await Expect(dialog).ToContainTextAsync("The referrer asked for: Urgent");
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Continue" })).ToBeDisabledAsync();
        await dialog.GetByRole(AriaRole.Combobox, new() { Name = "Template" }).ClickAsync();
        await page.GetByRole(AriaRole.Option, new() { Name = template.Name }).ClickAsync();
        await dialog.GetByRole(AriaRole.Combobox, new() { Name = "Priority" }).ClickAsync();
        await page.GetByRole(AriaRole.Option, new() { Name = "High", Exact = true }).ClickAsync();
        var dueDate = UkToday.AddDays(21);
        await dialog.GetByRole(AriaRole.Combobox, new() { Name = "Target Date" }).FillAsync(Typed(dueDate));
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();

        // The check shows the patient, the specialty and the template before anything is saved.
        var check = page.GetByRole(AriaRole.Dialog, new() { Name = "Check and Confirm" });
        await Expect(check).ToContainTextAsync(patient.LastName, new() { IgnoreCase = true });
        await Expect(check).ToContainTextAsync(specialty.Name);
        await Expect(check).ToContainTextAsync(template.Name);
        await check.GetByRole(AriaRole.Button, new() { Name = "Accept Referral" }).ClickAsync();

        // The pathway opens, linked back to its referral.
        await Expect(page).ToHaveURLAsync(new Regex(@"/pathways/[0-9a-f-]{36}$"));
        var pathwayId = Guid.Parse(new Uri(page.Url).Segments[^1]);
        await Expect(main.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync(template.Name);
        await Expect(main.GetByRole(AriaRole.Link, new() { NameRegex = new Regex("^From a referral received") })).ToBeVisibleAsync();

        var saved = await Api.FindReferralAsync(referral.Id);
        var pathway = await Api.FindPathwayAsync(pathwayId);
        Assert.Multiple(() =>
        {
            Assert.That(saved!.Status, Is.EqualTo("Accepted"));
            Assert.That(saved.PathwayId, Is.EqualTo(pathwayId));
            Assert.That(saved.DecidedByUsername, Is.EqualTo(username));
            Assert.That(pathway!.Priority, Is.EqualTo("High"));
            Assert.That(pathway.DueDate, Is.EqualTo(dueDate));
            Assert.That(pathway.Referral!.Id, Is.EqualTo(referral.Id));
        });

        // Whoever registered it (the E2E admin) is told.
        var told = (await Api.NotificationsAsync()).Items.SingleOrDefault(n => n.ReferralId == referral.Id);
        Assert.That(told, Is.Not.Null);
        Assert.That(told!.Kind, Is.EqualTo("ReferralTriaged"));
        await Api.MarkNotificationReadAsync(told.Id);
    }
}
