using System.Text.RegularExpressions;
using MedApp.E2ETests.Common;
using Microsoft.Playwright;

namespace MedApp.E2ETests.Calendar;

/// <summary>Booking an appointment from the patient page, finding it on the Calendar, and cancelling it there.</summary>
[TestFixture]
public sealed class BookingTests : E2ETest
{
    private const string CancelReason = "Synthetic cancellation from the end-to-end tests.";

    [Test]
    public async Task BookAppointment_FromThePatientPage_ShowsOnTheCalendarAndCanBeCancelled()
    {
        var patient = await CreatePatientAsync();
        var me = (await Api.WhoAmIAsync()).Name!;

        // From the patient page: with me, for 30 minutes, the first day with free time and its first free time.
        await OpenAsync($"/patients/{patient.Id}");
        await Main.GetByRole(AriaRole.Button, new() { Name = "Book Appointment" }).ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog, new() { Name = "Book Appointment" });
        await Expect(dialog).ToContainTextAsync(patient.LastName);
        // With is a plain list, or one with a search box (a button) once there are more than eight staff.
        var with = dialog.GetByRole(AriaRole.Combobox, new() { Name = "With" })
            .Or(dialog.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^With ") }));
        await Expect(with).ToContainTextAsync($"{me} (you)");
        await Expect(dialog.GetByRole(AriaRole.Combobox, new() { Name = "Length" })).ToContainTextAsync("30 minutes");

        var days = dialog.GetByRole(AriaRole.Group, new() { NameRegex = new Regex("^Days in the week of") });
        await days.GetByRole(AriaRole.Button, new() { Disabled = false }).First.ClickAsync();
        var times = dialog.GetByRole(AriaRole.Group, new() { NameRegex = new Regex("^(Morning|Afternoon) times on") });
        var firstTime = times.GetByRole(AriaRole.Button).First;
        // Its text is the time (twice: Astryx keeps a hidden copy to size the button).
        var time = Regex.Match((await firstTime.TextContentAsync())!, @"\d\d:\d\d").Value;
        await firstTime.ClickAsync();
        await Expect(dialog).ToContainTextAsync(new Regex($@", {Regex.Escape(time)} to \d\d:\d\d with {Regex.Escape(me)}"));
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Book", Exact = true }).ClickAsync();
        await Expect(dialog).ToBeHiddenAsync();

        // It's booked with me, and the patient page lists it.
        await Expect(Main.GetByRole(AriaRole.List, new() { Name = "Upcoming appointments" })).ToContainTextAsync("Booked");
        var appointment = (await Api.AppointmentsOfPatientAsync(patient.Id)).Single();
        Assert.Multiple(() =>
        {
            Assert.That(appointment.Status, Is.EqualTo("Booked"));
            Assert.That(appointment.StaffUsername, Is.EqualTo(me));
            Assert.That(appointment.DurationMinutes, Is.EqualTo(30));
            Assert.That(UkTime(appointment.StartUtc!.Value).ToString("HH:mm"), Is.EqualTo(time));
        });

        // On my calendar, that day: open it and cancel it, with a reason.
        var day = DateOnly.FromDateTime(UkTime(appointment.StartUtc!.Value));
        await OpenAsync($"/calendar?date={day:yyyy-MM-dd}&view=day");
        var onCalendar = Main.GetByRole(AriaRole.Button, new()
        {
            NameRegex = new Regex($"{Regex.Escape(patient.LastName)}.*{Regex.Escape(time)} to .*Booked"),
        });
        await Expect(onCalendar).ToBeVisibleAsync();
        // A clickable card: its button is for the keyboard and screen readers, and the card itself takes the click.
        await onCalendar.Locator("xpath=..").ClickAsync();
        var details = Page.GetByRole(AriaRole.Dialog, new() { Name = "Appointment" });
        await Expect(details).ToContainTextAsync(patient.LastName);
        await details.GetByRole(AriaRole.Button, new() { Name = "Cancel Appointment" }).ClickAsync();

        var cancel = Page.GetByRole(AriaRole.Dialog, new() { Name = "Cancel Appointment" });
        var confirm = cancel.GetByRole(AriaRole.Button, new() { Name = "Cancel Appointment" });
        await Expect(confirm).ToBeDisabledAsync(); // a reason first
        await cancel.GetByRole(AriaRole.Textbox, new() { Name = "Reason" }).FillAsync(CancelReason);
        await confirm.ClickAsync();
        await Expect(cancel).ToBeHiddenAsync();

        // Cancelled appointments leave the calendar.
        await Expect(onCalendar).ToBeHiddenAsync();
        var cancelled = (await Api.AppointmentsOfPatientAsync(patient.Id)).Single();
        Assert.Multiple(() =>
        {
            Assert.That(cancelled.Status, Is.EqualTo("Cancelled"));
            Assert.That(cancelled.CancelReason, Is.EqualTo(CancelReason));
        });
    }

    private static DateTime UkTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById("Europe/London"));
}
