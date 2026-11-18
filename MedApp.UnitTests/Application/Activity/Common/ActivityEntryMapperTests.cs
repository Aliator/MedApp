using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Domain.Activity;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Application.Activity.Common;

[TestFixture]
public sealed class ActivityEntryMapperTests
{
    private static ActivityEntry Entry(Guid? patientId) => new()
    {
        Id = Guid.NewGuid(),
        AtUtc = ActivityTestConstants.Now,
        Action = ActivityAction.PatientViewed,
        UserId = ActivityTestConstants.UserId,
        Username = ActivityTestConstants.Username,
        IpAddress = ActivityTestConstants.IpAddress,
        PatientId = patientId,
        PathwayId = Guid.NewGuid(),
        TargetUsername = "target",
        Details = """{"reason":"x"}"""
    };

    [Test]
    public void ToResponse_KnownPatient_CopiesEveryField_AndNamesThem()
    {
        var patientId = Guid.NewGuid();
        var entry = Entry(patientId);

        var response = ActivityEntryMapper.ToResponse(entry, new Dictionary<Guid, string> { [patientId] = "Test Patient" });

        response.Id.Should().Be(entry.Id);
        response.AtUtc.Should().Be(entry.AtUtc);
        response.Action.Should().Be("PatientViewed");
        response.UserId.Should().Be(entry.UserId);
        response.Username.Should().Be(entry.Username);
        response.IpAddress.Should().Be(entry.IpAddress);
        response.PatientId.Should().Be(patientId);
        response.PatientName.Should().Be("Test Patient");
        response.PathwayId.Should().Be(entry.PathwayId);
        response.TargetUsername.Should().Be("target");
        response.Details.Should().Be(entry.Details);
    }

    [Test]
    public void ToResponse_PatientNoLongerThere_IsADeletedPatient()
    {
        ActivityEntryMapper.ToResponse(Entry(Guid.NewGuid())).PatientName.Should().Be(ActivityEntryMapper.DeletedPatient);
    }

    [Test]
    public void ToResponse_NoPatient_HasNoName()
    {
        ActivityEntryMapper.ToResponse(Entry(null)).PatientName.Should().BeNull();
    }
}
