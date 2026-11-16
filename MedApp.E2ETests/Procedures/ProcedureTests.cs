using System.Text.RegularExpressions;
using MedApp.E2ETests.Common;
using Microsoft.Playwright;

namespace MedApp.E2ETests.Procedures;

/// <summary>A procedure for a patient: made from a template, then its stages worked in order.</summary>
[TestFixture]
public sealed class ProcedureTests : E2ETest
{
    private const string StageNotes = "Synthetic stage notes from the end-to-end tests.";

    [Test]
    public async Task NewProcedure_FromATemplate_ThenItsStagesWorkedToInReview()
    {
        var (template, stages) = await CreateTemplateAsync(2);
        var patient = await CreatePatientAsync();
        var dueDate = UkToday.AddDays(7);

        // From the patient's procedures, a new one from the template.
        await OpenAsync($"/patients/{patient.Id}/procedures");
        await Main.GetByRole(AriaRole.Link, new() { Name = "New Procedure" }).ClickAsync();
        await Main.GetByRole(AriaRole.Combobox, new() { Name = "Template" }).ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = template.Name }).ClickAsync();
        // Picking it shows the stages the procedure will copy.
        await Expect(Main).ToContainTextAsync($"1. {stages[0].Name}");
        await Expect(Main).ToContainTextAsync($"2. {stages[1].Name}");
        await Main.GetByRole(AriaRole.Combobox, new() { Name = "Due Date" }).FillAsync(Typed(dueDate));
        await Main.GetByRole(AriaRole.Button, new() { Name = "Create Procedure" }).ClickAsync();
        await Page.GetByRole(AriaRole.Dialog, new() { Name = "Confirm New Procedure" })
            .GetByRole(AriaRole.Button, new() { Name = "Create Procedure" }).ClickAsync();

        // Its page: titled after the template, at the first stage.
        await Expect(Page).ToHaveURLAsync(new Regex(@"/procedures/[0-9a-f-]{36}$"));
        var procedureId = Guid.Parse(new Uri(Page.Url).Segments[^1]);
        await Expect(Main.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync(template.Name);
        await Expect(Main).ToContainTextAsync("Unassigned");
        await Expect(Main).ToContainTextAsync("0 of 2");

        // Notes on the first stage start it, and the procedure.
        await Main.GetByRole(AriaRole.Textbox, new() { Name = "Notes" }).ClickAsync();
        await Page.Keyboard.TypeAsync(StageNotes);
        await Main.GetByRole(AriaRole.Button, new() { Name = "Save Notes" }).ClickAsync();
        await Expect(Main.GetByRole(AriaRole.Button, new() { Name = "Save Notes" })).ToBeDisabledAsync();
        await Expect(Main).ToContainTextAsync("In progress");

        // Finishing it moves on to the second stage.
        await FinishCurrentStageAsync(stages[0].Name);
        await Expect(Main).ToContainTextAsync($"2. {stages[1].Name}");
        await Expect(Main).ToContainTextAsync("1 of 2");

        // Finishing the last stage sends the procedure for review.
        await FinishCurrentStageAsync(stages[1].Name);
        await Expect(Main).ToContainTextAsync("In review");
        await Expect(Main.GetByRole(AriaRole.Button, new() { Name = "Mark Completed" })).ToBeVisibleAsync();

        var saved = await Api.FindProcedureAsync(procedureId);
        Assert.That(saved, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(saved!.Status, Is.EqualTo("InReview"));
            Assert.That(saved.PatientId, Is.EqualTo(patient.Id));
            Assert.That(saved.DueDate, Is.EqualTo(dueDate));
            Assert.That(saved.Stages.Select(s => s.Name), Is.EqualTo(stages.Select(s => s.Name)));
            Assert.That(saved.Stages[0].Notes, Does.Contain(StageNotes));
            Assert.That(saved.Stages.All(s => s.IsLocked), Is.True);
        });
    }

    private async Task FinishCurrentStageAsync(string stageName)
    {
        await Main.GetByRole(AriaRole.Button, new() { Name = "Finish Stage" }).ClickAsync();
        var confirm = Page.GetByRole(AriaRole.Dialog, new() { Name = $"Finish {stageName}?" });
        await confirm.GetByRole(AriaRole.Button, new() { Name = "Finish Stage" }).ClickAsync();
        await Expect(confirm).ToBeHiddenAsync();
    }
}
