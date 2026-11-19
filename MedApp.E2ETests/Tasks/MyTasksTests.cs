using System.Text.RegularExpressions;
using MedApp.E2ETests.Common;
using Microsoft.Playwright;

namespace MedApp.E2ETests.Tasks;

/// <summary>My Worklist: the pathways assigned to you, not started ones first.</summary>
[TestFixture]
public sealed class MyTasksTests : E2ETest
{
    [Test]
    public async Task AssignedPathway_IsListedUntilStartedThenUnderStarted()
    {
        var (template, stages) = await CreateTemplateAsync(1);
        var patient = await CreatePatientAsync();
        var pathway = await CreatePathwayAsync(patient.Id, template.Id);
        await Api.AssignAsync(pathway.Id, Staff.UserId);
        var title = Uri.EscapeDataString(pathway.Title);

        // Not started: the list's default.
        await OpenAsync($"/tasks?title={title}");
        await Expect(Main.GetByRole(AriaRole.Heading, new() { Name = "My Worklist" })).ToBeVisibleAsync();
        var row = Main.GetByRole(AriaRole.Row).Filter(new() { HasText = pathway.Title });
        await Expect(row).ToHaveCountAsync(1);
        await Expect(row).ToContainTextAsync(patient.LastName);
        await Expect(row).ToContainTextAsync(template.Name);

        // Started, it moves to Started.
        await Api.SaveStageNotesAsync(pathway.Id, pathway.Stages[0].Id, "Synthetic notes.");
        await Page.ReloadAsync();
        await WaitUntilInteractiveAsync();
        await Expect(Main.GetByText("No pathways match your search")).ToBeVisibleAsync();
        await OpenAsync($"/tasks?progress=started&title={title}");
        await Expect(row).ToHaveCountAsync(1);
        await Expect(row).ToContainTextAsync(stages[0].Name);

        // And it opens the pathway.
        await row.GetByRole(AriaRole.Link, new() { Name = pathway.Title }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"/pathways/{pathway.Id}$"));
        await Expect(Main.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync(pathway.Title);
    }
}
