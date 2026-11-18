using System.Buffers.Text;
using System.Text;
using FluentAssertions;
using MedApp.Application.Pathways.Timeline.Common;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using static MedApp.UnitTests.Common.Constants.TimelineTestConstants;

namespace MedApp.UnitTests.Application.Pathways.Timeline.Common;

[TestFixture]
public sealed class TimelineCursorTests
{
    [Test]
    public void Encode_ThenTryDecode_ReturnsTheSameCursor()
    {
        var cursor = new TimelineCursor(Now.AddTicks(1234567), PathwayId);

        TimelineCursor.TryDecode(cursor.Encode(), out var decoded).Should().BeTrue();

        decoded.Should().Be(cursor);
        decoded.AtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Test]
    public void After_Entry_IsItsTimeAndId()
    {
        var entry = CreateEntry(MedApp.Domain.Pathways.PatientPathways.PatientPathwayAuditAction.Created, Now);

        TimelineCursor.After(entry).Should().Be(new TimelineCursor(Now, entry.Id));
    }

    [Test]
    public void Encode_IsUrlSafe()
    {
        new TimelineCursor(Now, PathwayId).Encode().Should().MatchRegex("^[A-Za-z0-9_-]+$");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    [TestCase("not base64!")]
    public void TryDecode_NotACursor_ReturnsFalse(string? value)
    {
        TimelineCursor.TryDecode(value, out _).Should().BeFalse();
    }

    [TestCase("abc")]
    [TestCase("1.2.3")]
    [TestCase("-1.00000022000000000000000000000001")]
    [TestCase("1.not-a-guid")]
    [TestCase("99999999999999999999.00000022000000000000000000000001")]
    [TestCase("3155378976000000000.00000022000000000000000000000001")]
    public void TryDecode_WellEncodedButWrongInside_ReturnsFalse(string text)
    {
        TimelineCursor.TryDecode(Base64Url.EncodeToString(Encoding.UTF8.GetBytes(text)), out _).Should().BeFalse();
    }

    [Test]
    public void TryDecode_TooLong_ReturnsFalse()
    {
        TimelineCursor.TryDecode(new string('A', 101), out _).Should().BeFalse();
    }

    [Test]
    public void After_AppointmentEvent_IsItsTimeAndId()
    {
        var entry = CreateAppointmentEvent(MedApp.Domain.Appointments.AppointmentEventAction.Booked, Now);

        TimelineCursor.After(entry).Should().Be(new TimelineCursor(Now, entry.Id));
    }
}
