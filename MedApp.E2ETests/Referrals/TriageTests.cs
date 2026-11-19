using System.Text.RegularExpressions;
using MedApp.E2ETests.Common;
using Microsoft.Playwright;

namespace MedApp.E2ETests.Referrals;

/// <summary>
/// Triage: a referral to the E2E clinic, registered by the run's member of staff, is accepted by a Clinician of the
/// clinic, in a browser of their own, choosing the template, priority and target date (nothing is prefilled); the
/// pathway opens, linked back to the referral, and whoever registered it is told.
/// </summary>
[TestFixture]
public sealed class TriageTests : E2ETest
{
    [Test]
    public async Task Accept_AsAClinicianOfItsTeam_CreatesThePathwayLinkedToTheReferral()
    {
        var (template, _) = await CreateTemplateAsync(2);

        // A patient with a referral can't be deleted: they stay, with the referral (README); their pathway goes.
        var patient = await Api.CreatePatientAsync("Grace", E2EData.UniqueName("Patient"), new DateOnly(1984, 3, 21));
        Data.OnCleanUp($"the pathways of {patient.LastName}", () => DeletePathwaysOfAsync(patient.Id));
        var referral = await Api.RegisterReferralAsync(patient.Id, Team.Id, "Synthetic reason for referral from the end-to-end tests.");

        // A Clinician who belongs to the clinic signs in, in a browser of their own, and opens the referral.
        var clinician = await CreateColleagueAsync(["Clinician"]);
        var page = await SignInPageAsync(clinician.Username, clinician.Password);
        await OpenAsync(page, $"/referrals/{referral.Id}");
        var main = page.GetByRole(AriaRole.Main);

        // Accept: the template and priority are chosen, never prefilled; the target date comes from the waiting-time clock.
        await main.GetByRole(AriaRole.Button, new() { Name = "Accept" }).ClickAsync();
        var dialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Accept Referral" });
        await Expect(dialog).ToContainTextAsync("The referrer asked for: Urgent");
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Continue" })).ToBeDisabledAsync();
        await dialog.GetByRole(AriaRole.Combobox, new() { Name = "Template" }).ClickAsync();
        await page.GetByRole(AriaRole.Option, new() { Name = template.Name }).ClickAsync();
        await dialog.GetByRole(AriaRole.Combobox, new() { Name = "Priority" }).ClickAsync();
        await page.GetByRole(AriaRole.Option, new() { Name = "High", Exact = true }).ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();

        // The check shows the patient, the team and the template before anything is saved.
        var check = page.GetByRole(AriaRole.Dialog, new() { Name = "Check and Confirm" });
        await Expect(check).ToContainTextAsync(patient.LastName, new() { IgnoreCase = true });
        await Expect(check).ToContainTextAsync(Team.Name);
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
            Assert.That(saved.DecidedByUsername, Is.EqualTo(clinician.Username));
            Assert.That(pathway!.Priority, Is.EqualTo("High"));
            Assert.That(saved.Clock!.TargetDays, Is.EqualTo(Team.TargetDaysHigh));
            Assert.That(pathway.DueDate, Is.EqualTo(saved.Clock.TargetOn));
            Assert.That(pathway.Referral!.Id, Is.EqualTo(referral.Id));
        });

        // Whoever registered it (the run's member of staff) is told.
        var told = (await Api.NotificationsAsync()).Items.SingleOrDefault(n => n.ReferralId == referral.Id);
        Assert.That(told, Is.Not.Null);
        Assert.That(told!.Kind, Is.EqualTo("ReferralTriaged"));
        await Api.MarkNotificationReadAsync(told.Id);
    }
}
