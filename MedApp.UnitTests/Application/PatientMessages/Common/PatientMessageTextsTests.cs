using FluentAssertions;
using MedApp.Application.PatientMessages.Common;
using MedApp.Domain.PatientMessages;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.PatientMessagesTestConstants;

namespace MedApp.UnitTests.Application.PatientMessages.Common;

[TestFixture]
public sealed class PatientMessageTextsTests
{
    private static readonly DateTime Start = At(NextMonday, 10);

    [Test]
    public void Body_ReminderText_IsOneSmsWithTheSiteAndPostcodeButNotTheStreet()
    {
        var text = PatientMessageTexts.Body(
            PatientMessageKind.AppointmentReminder, PatientMessageChannel.Text, SenderName, Start, Site, ContactPhone);

        text.Should().Be(
            "Riverside Test Clinic: reminder of your appointment on Mon 23 Nov at 10:00 at Hilltop Outreach, ZZ1 1ZZ. To change it, call 01632 960123.");
        text.Length.Should().BeLessThanOrEqualTo(PatientMessageTexts.TextLength);
    }

    [Test]
    public void Body_ReminderEmail_HasTheWholeAddressAndTheFooter()
    {
        var email = PatientMessageTexts.Body(
            PatientMessageKind.AppointmentReminder, PatientMessageChannel.Email, SenderName, Start, Site, ContactPhone);

        email.Should().Be(
            "Riverside Test Clinic: reminder of your appointment on Mon 23 Nov at 10:00 at Hilltop Outreach, 1 Test Street, Testtown, ZZ1 1ZZ. To change it, call 01632 960123."
            + "\n\n" + PatientMessageTexts.EmailFooter);
    }

    [Test]
    public void Body_SiteWithoutPhone_AsksForTheContactPhone()
    {
        var text = PatientMessageTexts.Body(
            PatientMessageKind.MissedAppointment, PatientMessageChannel.Text, SenderName, Start, SiteWithoutPhone, ContactPhone);

        text.Should().Be(
            "Riverside Test Clinic: we missed you at your appointment on Mon 23 Nov at 10:00 at Hilltop Outreach. Please call 01632 960000 to arrange another.");
    }

    [Test]
    public void Body_NoSiteAndNoPhone_SaysContactUs()
    {
        PatientMessageTexts.Body(PatientMessageKind.AppointmentMoved, PatientMessageChannel.Text, SenderName, Start, null, null)
            .Should().Be("Riverside Test Clinic: your appointment has moved to Mon 23 Nov at 10:00. To change it, please contact us.");
        PatientMessageTexts.Body(PatientMessageKind.AppointmentCancelled, PatientMessageChannel.Text, SenderName, Start, null, " ")
            .Should().Be("Riverside Test Clinic: your appointment on Mon 23 Nov at 10:00 has been cancelled. Please contact us to arrange another.");
    }

    [Test]
    public void Body_SummerTime_IsInUkTime()
    {
        var summer = new DateTime(2026, 7, 14, 9, 30, 0, DateTimeKind.Utc);

        PatientMessageTexts.When(summer).Should().Be("Tue 14 Jul at 10:30");
    }

    [Test]
    public void Body_EveryKindAndChannel_HoldsOnlyTheFixedWordsDateTimeAndPlace()
    {
        foreach (var kind in Enum.GetValues<PatientMessageKind>())
        foreach (var channel in Enum.GetValues<PatientMessageChannel>())
        {
            var body = PatientMessageTexts.Body(kind, channel, SenderName, Start, Site, ContactPhone);

            body.Should().StartWith(SenderName + ": ").And.Contain("Mon 23 Nov at 10:00").And.Contain(Site.Name);
            body.Should().NotContainAny("pathway", "stage", "Dr ", "result", "clinic letter");
            if (channel == PatientMessageChannel.Text)
                body.Length.Should().BeLessThanOrEqualTo(PatientMessageTexts.TextLength);
        }
    }

    [TestCase(PatientMessageKind.AppointmentReminder, "Appointment reminder")]
    [TestCase(PatientMessageKind.AppointmentMoved, "Your appointment has moved")]
    [TestCase(PatientMessageKind.AppointmentCancelled, "Your appointment has been cancelled")]
    [TestCase(PatientMessageKind.MissedAppointment, "We missed you at your appointment")]
    public void Subject_EachKind_IsAFixedPhrase(PatientMessageKind kind, string subject) =>
        PatientMessageTexts.Subject(kind).Should().Be(subject);
}
