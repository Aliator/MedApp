using System.Text.RegularExpressions;
using MedApp.E2ETests.Common;
using Microsoft.Playwright;

namespace MedApp.E2ETests.Letters;

/// <summary>
/// A letter from start to sent: a Clinician (letters are clinical, so not the E2E admin), in a browser of their own,
/// writes one on a pathway from a template, approves it themselves (its PDF made), and records it as posted.
/// </summary>
[TestFixture]
public sealed class LetterTests : E2ETest
{
    private static readonly string ChosenPassword = E2EData.NewPassword();

    [Test]
    public async Task Letter_IsWrittenFromATemplate_Approved_AndRecordedAsPosted()
    {
        // Its pathway stays (cancelled), so its template and stages are archived after, not deleted.
        var (template, _) = await CreateTemplateForAKeptPathwayAsync(2);
        var patient = await Api.CreatePatientAsync("Grace", E2EData.UniqueName("Patient"), new DateOnly(1984, 3, 21));
        var pathway = await Api.CreatePathwayAsync(patient.Id, template.Id, E2EData.UniqueName("Pathway"), UkToday.AddDays(14));
        // A pathway with a letter can't be deleted (it's clinical correspondence): it stays, cancelled, with its
        // patient (README).
        Data.OnCleanUp($"pathway {pathway.Id}", () => Api.CancelPathwayAsync(pathway.Id));
        var letterTemplate = await Api.CreateLetterTemplateAsync(E2EData.UniqueName("Letter"));
        Data.OnCleanUp($"letter template {letterTemplate.Name}", () => Api.ArchiveOrDeleteLetterTemplateAsync(letterTemplate.Id));

        // A Clinician with a full name (printed on what they approve) and a password of their own.
        var username = E2EData.UniqueName("clinician").Replace(' ', '-').ToLowerInvariant();
        var firstPassword = await Api.CreateUserAsync(username, "Clinician");
        Data.OnCleanUp($"user {username}", () => Api.DeleteUserAsync(username, E2ESettings.Password!));
        await Api.SetStaffDetailsAsync(username, "Dr Synthetic Clinician", "Consultant");
        await (await SignInToApiAsync(username, firstPassword)).UpdateOwnPasswordAsync(username, firstPassword, ChosenPassword);

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

        // From the pathway's Letters, a letter from the template: nothing is pre-selected.
        await OpenAsync(page, $"/pathways/{pathway.Id}");
        var main = page.GetByRole(AriaRole.Main);
        await main.GetByRole(AriaRole.Button, new() { Name = "Write Letter" }).ClickAsync();
        var write = page.GetByRole(AriaRole.Dialog, new() { Name = "Write Letter" });
        await Expect(write.GetByRole(AriaRole.Button, new() { Name = "Start Letter" })).ToBeDisabledAsync();
        await write.GetByRole(AriaRole.Combobox, new() { Name = "Template" }).ClickAsync();
        await page.GetByRole(AriaRole.Option, new() { NameRegex = new Regex(Regex.Escape(letterTemplate.Name)) }).ClickAsync();
        await write.GetByRole(AriaRole.Button, new() { Name = "Start Letter" }).ClickAsync();

        // The draft's page: the template's text, theirs to write.
        await Expect(page).ToHaveURLAsync(new Regex(@"/letters/[0-9a-f-]{36}$"));
        var letterId = Guid.Parse(new Uri(page.Url).Segments[^1]);
        var body = main.GetByRole(AriaRole.Textbox, new() { Name = "Body" });
        await Expect(body).ToHaveValueAsync(new Regex("Dear Doctor"));
        await body.ClickAsync();
        await page.Keyboard.PressAsync("Control+End");
        await page.Keyboard.TypeAsync("\n\nSynthetic plan from the end-to-end tests: review in six weeks.");
        await main.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await Expect(main.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true })).ToBeDisabledAsync();

        // Approved by its author, a clinician: the check shows the patient and every recipient first.
        await main.GetByRole(AriaRole.Button, new() { Name = "Approve", Exact = true }).ClickAsync();
        var approve = page.GetByRole(AriaRole.Dialog, new() { Name = "Approve Letter?" });
        await Expect(approve).ToContainTextAsync(patient.LastName, new() { IgnoreCase = true });
        await approve.GetByRole(AriaRole.Button, new() { Name = "Approve", Exact = true }).ClickAsync();
        await Expect(approve).ToBeHiddenAsync();
        await Expect(main.GetByRole(AriaRole.Link, new() { Name = "Download PDF" })).ToBeVisibleAsync();

        // Posted to each recipient, recorded.
        var recipients = (await Api.FindLetterAsync(letterId))!.Recipients.Count;
        for (var i = 0; i < recipients; i++)
        {
            await main.GetByRole(AriaRole.Button, new() { Name = "Record as Sent" }).First.ClickAsync();
            var record = page.GetByRole(AriaRole.Dialog, new() { Name = "Record as Sent" });
            await record.GetByRole(AriaRole.Radio, new() { Name = "Post", Exact = true }).ClickAsync();
            await record.GetByRole(AriaRole.Button, new() { Name = "Record as Sent" }).ClickAsync();
            await Expect(record).ToBeHiddenAsync();
        }

        await Expect(main.GetByRole(AriaRole.Button, new() { Name = "Record as Sent" })).ToHaveCountAsync(0);

        var saved = await Api.FindLetterAsync(letterId);
        Assert.That(saved, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(saved!.Status, Is.EqualTo("Approved"));
            Assert.That(saved.AuthorUsername, Is.EqualTo(username));
            Assert.That(saved.ApproverUsername, Is.EqualTo(username));
            Assert.That(saved.PdfAttachmentId, Is.Not.Null);
            Assert.That(saved.Body, Does.Contain("Synthetic plan from the end-to-end tests"));
            Assert.That(saved.Recipients.Select(r => r.DispatchStatus), Is.All.EqualTo("Sent"));
            Assert.That(saved.Recipients.Select(r => r.Method), Is.All.EqualTo("Post"));
        });
    }
}
