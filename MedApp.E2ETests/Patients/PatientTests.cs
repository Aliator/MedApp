using System.Text.RegularExpressions;
using MedApp.E2ETests.Common;
using Microsoft.Playwright;

namespace MedApp.E2ETests.Patients;

/// <summary>The patients list and forms: add, find with the filter bar, edit, and delete with confirmation.</summary>
[TestFixture]
public sealed class PatientTests : E2ETest
{
    private static readonly DateOnly DateOfBirth = new(1984, 3, 21);

    [Test]
    public async Task AddPatient_ThroughTheForm_IsFoundWithTheFilterBar()
    {
        var lastName = E2EData.UniqueName("Patient");
        Data.OnCleanUp($"patient {lastName}", async () =>
        {
            foreach (var patient in await Api.SearchPatientsByLastNameAsync(lastName))
                await Api.DeletePatientAsync(patient.Id);
        });
        await OpenAsync("/patients");

        await Main.GetByRole(AriaRole.Link, new() { Name = "Add Patient" }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Add New Patient" })).ToBeVisibleAsync();
        await Main.GetByLabel("First Name").FillAsync("Ada");
        await Main.GetByLabel("Last Name").FillAsync(lastName);
        await Main.GetByRole(AriaRole.Combobox, new() { Name = "Date of Birth" }).FillAsync(Typed(DateOfBirth));
        await Main.GetByLabel("Email Address").FillAsync("ada.e2e@example.test");
        await Main.GetByRole(AriaRole.Button, new() { Name = "Add Patient" }).ClickAsync();
        var confirm = Page.GetByRole(AriaRole.Dialog, new() { Name = "Confirm Add Patient" });
        await confirm.GetByRole(AriaRole.Button, new() { Name = "Confirm Add" }).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex("/patients$"));
        await FilterByLastNameAsync(lastName);

        // The filter is kept in the URL.
        await Expect(Page).ToHaveURLAsync(new Regex("[?&]last="));
        await Expect(Main.GetByText("1 matching patient")).ToBeVisibleAsync();
        var row = Main.GetByRole(AriaRole.Row).Filter(new() { HasText = lastName });
        await Expect(row).ToHaveCountAsync(1);
        await Expect(row).ToContainTextAsync("21 Mar 1984");
        await Expect(row).ToContainTextAsync("ada.e2e@example.test");
    }

    [Test]
    public async Task EditPatient_ChangedFields_AreSaved()
    {
        var patient = await CreatePatientAsync();
        var newLastName = E2EData.UniqueName("Renamed");
        await OpenAsync($"/patients/{patient.Id}/edit");

        await Main.GetByLabel("Last Name").FillAsync(newLastName);
        await Main.GetByLabel("Email Address").FillAsync("renamed.e2e@example.test");
        await Main.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Page.GetByRole(AriaRole.Dialog, new() { Name = "Confirm Changes" })
            .GetByRole(AriaRole.Button, new() { Name = "Confirm Save" }).ClickAsync();

        // Back on the patient's page, showing the changes.
        await Expect(Page).ToHaveURLAsync(new Regex($"/patients/{patient.Id}$"));
        await Expect(Main).ToContainTextAsync(newLastName);
        await Expect(Main).ToContainTextAsync("renamed.e2e@example.test");
        var saved = await Api.FindPatientAsync(patient.Id);
        Assert.That(saved?.LastName, Is.EqualTo(newLastName));
    }

    [Test]
    public async Task DeletePatient_Confirmed_RemovesThemFromTheList()
    {
        var patient = await CreatePatientAsync();
        await OpenAsync($"/patients/{patient.Id}/edit");

        await Main.GetByRole(AriaRole.Button, new() { Name = "Delete Patient" }).ClickAsync();
        var confirm = Page.GetByRole(AriaRole.Dialog, new() { Name = "Confirm Delete" });
        await Expect(confirm).ToContainTextAsync("This action cannot be undone.");
        await confirm.GetByRole(AriaRole.Button, new() { Name = "Delete Patient" }).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex("/patients$"));
        // Searched for by name (in the URL: with no patients at all, the list has no filter bar to type in).
        await OpenAsync($"/patients?last={Uri.EscapeDataString(patient.LastName)}");
        await Expect(Main.GetByText("No patients match your search")).ToBeVisibleAsync();
        Assert.That(await Api.FindPatientAsync(patient.Id), Is.Null);
    }

    [Test]
    public async Task DeletePatient_Cancelled_KeepsThem()
    {
        var patient = await CreatePatientAsync();
        await OpenAsync($"/patients/{patient.Id}/edit");

        await Main.GetByRole(AriaRole.Button, new() { Name = "Delete Patient" }).ClickAsync();
        var confirm = Page.GetByRole(AriaRole.Dialog, new() { Name = "Confirm Delete" });
        await confirm.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();

        await Expect(confirm).ToBeHiddenAsync();
        Assert.That(await Api.FindPatientAsync(patient.Id), Is.Not.Null);
    }

    /// <summary>Adds "Last name contains ..." to the list's filter bar.</summary>
    private async Task FilterByLastNameAsync(string lastName)
    {
        await (await OpenListSearchAsync("Search patients")).ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = "Last name", Exact = true }).ClickAsync();
        await Main.GetByRole(AriaRole.Textbox, new() { Name = "Value" }).FillAsync(lastName);
        await Main.GetByRole(AriaRole.Button, new() { Name = "Apply" }).ClickAsync();
    }
}
