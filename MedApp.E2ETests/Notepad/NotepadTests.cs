using MedApp.E2ETests.Common;
using Microsoft.Playwright;

namespace MedApp.E2ETests.Notepad;

/// <summary>
/// The notepad: jotted in beside a procedure's page, then added to that procedure as a note, after the confirmation
/// that shows the patient. The E2E account's own notepad is put back as it was afterwards.
/// </summary>
[TestFixture]
public sealed class NotepadTests : E2ETest
{
    [Test]
    public async Task Notepad_WrittenIn_ThenAddedToTheOpenProcedure_ShowsAsItsNote()
    {
        // The account's notepad, put back afterwards; empty for the test, so all of it is the test's text.
        var before = await Api.NotepadAsync();
        Data.OnCleanUp("the notepad", async () => await Api.SaveNotepadAsync(before.Text));
        await Api.SaveNotepadAsync(string.Empty);

        var (template, _) = await CreateTemplateAsync(1);
        var patient = await CreatePatientAsync();
        var procedure = await CreateProcedureAsync(patient.Id, template.Id);
        var jotting = $"Synthetic jotting {E2EData.UniqueName("call")}: the patient rang to confirm.";

        await OpenAsync($"/procedures/{procedure.Id}");
        await Page.GetByRole(AriaRole.Navigation, new() { Name = "Side navigation" }).First
            .GetByRole(AriaRole.Button, new() { Name = "Notepad" }).ClickAsync();
        var notepad = Page.GetByRole(AriaRole.Region, new() { Name = "Notepad" });
        await Expect(notepad).ToBeVisibleAsync();

        // It saves by itself after a pause.
        await notepad.GetByRole(AriaRole.Textbox, new() { Name = "Notepad" }).ClickAsync();
        await Page.Keyboard.TypeAsync(jotting);
        await Expect(notepad).ToContainTextAsync("Saved ", new() { Timeout = 10_000 });
        Assert.That((await Api.NotepadAsync()).Text, Does.Contain(jotting));

        // Added to the procedure, after checking who the patient is.
        await notepad.GetByRole(AriaRole.Button, new() { Name = "Add Note to Procedure" }).ClickAsync();
        var confirm = Page.GetByRole(AriaRole.Dialog, new() { Name = "Add Note to Procedure" });
        await Expect(confirm.GetByRole(AriaRole.Region, new() { Name = $"Patient: {patient.LastName}, {patient.FirstName}" })).ToBeVisibleAsync();
        await Expect(confirm).ToContainTextAsync(procedure.Title);
        await Expect(confirm).ToContainTextAsync(jotting);
        await Expect(confirm.GetByRole(AriaRole.Checkbox, new() { Name = "Remove it from the notepad" })).ToBeCheckedAsync();
        await confirm.GetByRole(AriaRole.Button, new() { Name = "Add Note" }).ClickAsync();
        await Expect(confirm).ToBeHiddenAsync();

        // It shows on the procedure page at once (in its Notes list, not the notepad, which sits in the main area too),
        // and has left the notepad.
        await Expect(Main.GetByRole(AriaRole.List, new() { Name = "Notes" })).ToContainTextAsync(jotting);
        await Expect(notepad.GetByRole(AriaRole.Textbox, new() { Name = "Notepad" })).Not.ToContainTextAsync(jotting);

        var saved = await Api.FindProcedureAsync(procedure.Id);
        Assert.That(saved!.NoteList.Select(n => n.Text), Has.Some.Contains(jotting));
        // Taken out of the saved notepad too (saved straight after the note was added).
        var notepadText = (await Api.NotepadAsync()).Text;
        for (var tries = 0; notepadText.Contains(jotting) && tries < 20; tries++)
        {
            await Task.Delay(500);
            notepadText = (await Api.NotepadAsync()).Text;
        }

        Assert.That(notepadText, Does.Not.Contain(jotting));
    }
}
