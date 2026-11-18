using MedApp.E2ETests.Common;
using Microsoft.Playwright;

namespace MedApp.E2ETests.Calendar;

/// <summary>
/// The visit rule: a stage that requires a visit asks for one to be booked, and an admin can finish it without one,
/// giving a reason, which the patient's timeline shows.
/// </summary>
[TestFixture]
public sealed class StageVisitTests : E2ETest
{
    private const string Reason = "Synthetic reason from the end-to-end tests.";

    [Test]
    public async Task RequiredVisit_AdminFinishesWithoutOne_WithAReason()
    {
        var (template, stages) = await CreateTemplateAsync("Required", "None");
        var patient = await CreatePatientAsync();
        var procedure = await CreateProcedureAsync(patient.Id, template.Id);

        // Its first stage needs a visit, which the app has asked to be booked.
        await OpenAsync($"/procedures/{procedure.Id}");
        var visit = Main.GetByRole(AriaRole.Group, new() { Name = "Visit", Exact = true });
        await Expect(visit).ToContainTextAsync("Visit required");
        await Expect(visit).ToContainTextAsync("Requires appointment");
        await Expect(visit).ToContainTextAsync("the stage can't be finished until then");
        await Expect(visit.GetByRole(AriaRole.Button, new() { Name = "Book Visit" })).ToBeVisibleAsync();

        // As an admin, finishing it anyway asks why.
        await Main.GetByRole(AriaRole.Button, new() { Name = "Finish Stage" }).ClickAsync();
        var confirm = Page.GetByRole(AriaRole.Dialog, new() { Name = "Finish without a visit?" });
        await Expect(confirm).ToContainTextAsync($"{stages[0].Name} requires a visit");
        await Expect(confirm).ToContainTextAsync("Its visit comes off Requires Appointment.");
        var finish = confirm.GetByRole(AriaRole.Button, new() { Name = "Finish Without Visit" });
        await Expect(finish).ToBeDisabledAsync();
        await confirm.GetByRole(AriaRole.Textbox, new() { Name = "Reason" }).FillAsync(Reason);
        await finish.ClickAsync();
        await Expect(confirm).ToBeHiddenAsync();

        // The procedure moves on to the second stage; the first is finished, and its visit is off Requires Appointment.
        await Expect(Main).ToContainTextAsync($"2. {stages[1].Name}");
        await Expect(Main).ToContainTextAsync("1 of 2");
        await Expect(Main.GetByRole(AriaRole.Group, new() { Name = "Visit", Exact = true })).ToHaveCountAsync(0);

        var saved = await Api.FindProcedureAsync(procedure.Id);
        Assert.That(saved!.Stages[0].IsLocked, Is.True);
        var appointments = await Api.AppointmentsOfPatientAsync(patient.Id);
        Assert.That(appointments.Select(a => a.Status), Is.EqualTo(new[] { "Cancelled" }));

        // The patient's timeline shows it was finished without a visit, and (to an admin) why.
        await OpenAsync($"/patients/{patient.Id}");
        await Expect(Main).ToContainTextAsync($"Finished {stages[0].Name} without a visit");
        await Expect(Main).ToContainTextAsync(Reason);
    }
}
