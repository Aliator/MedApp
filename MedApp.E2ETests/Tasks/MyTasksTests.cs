using System.Text.RegularExpressions;
using MedApp.E2ETests.Common;
using Microsoft.Playwright;

namespace MedApp.E2ETests.Tasks;

/// <summary>My Tasks: the procedures assigned to you, not started ones first.</summary>
[TestFixture]
public sealed class MyTasksTests : E2ETest
{
    [Test]
    public async Task AssignedProcedure_IsListedUntilStartedThenUnderStarted()
    {
        var (template, stages) = await CreateTemplateAsync(1);
        var patient = await CreatePatientAsync();
        var procedure = await CreateProcedureAsync(patient.Id, template.Id);
        await Api.AssignAsync(procedure.Id, await Api.UserIdOfAsync(E2ESettings.Username!));
        var title = Uri.EscapeDataString(procedure.Title);

        // Not started: the list's default.
        await OpenAsync($"/tasks?title={title}");
        await Expect(Main.GetByRole(AriaRole.Heading, new() { Name = "My Tasks" })).ToBeVisibleAsync();
        var row = Main.GetByRole(AriaRole.Row).Filter(new() { HasText = procedure.Title });
        await Expect(row).ToHaveCountAsync(1);
        await Expect(row).ToContainTextAsync(patient.LastName);
        await Expect(row).ToContainTextAsync(template.Name);

        // Started, it moves to Started.
        await Api.SaveStageNotesAsync(procedure.Id, procedure.Stages[0].Id, "Synthetic notes.");
        await Page.ReloadAsync();
        await WaitUntilInteractiveAsync();
        await Expect(Main.GetByText("No procedures match your search")).ToBeVisibleAsync();
        await OpenAsync($"/tasks?progress=started&title={title}");
        await Expect(row).ToHaveCountAsync(1);
        await Expect(row).ToContainTextAsync(stages[0].Name);

        // And it opens the procedure.
        await row.GetByRole(AriaRole.Link, new() { Name = procedure.Title }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"/procedures/{procedure.Id}$"));
        await Expect(Main.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync(procedure.Title);
    }
}
