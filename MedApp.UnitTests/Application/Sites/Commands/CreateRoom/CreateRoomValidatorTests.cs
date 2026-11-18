using FluentAssertions;
using MedApp.Application.Sites.Commands.CreateRoom;
using static MedApp.UnitTests.Common.Constants.SitesTestConstants;

namespace MedApp.UnitTests.Application.Sites.Commands.CreateRoom;

[TestFixture]
public sealed class CreateRoomValidatorTests
{
    private readonly CreateRoomValidator _validator = new();

    [Test]
    public void Validate_SiteAndName_IsValid() =>
        _validator.Validate(new CreateRoomCommand(SiteId, RoomName)).IsValid.Should().BeTrue();

    [Test]
    public void Validate_NoSite_IsInvalid() =>
        _validator.Validate(new CreateRoomCommand(Guid.Empty, RoomName)).Errors.Should().ContainSingle(e => e.PropertyName == "SiteId");

    [TestCase("")]
    [TestCase("  ")]
    public void Validate_NoName_IsInvalid(string name) =>
        _validator.Validate(new CreateRoomCommand(SiteId, name)).Errors.Should().ContainSingle(e => e.PropertyName == "Name");

    [Test]
    public void Validate_NameTooLong_IsInvalid() =>
        _validator.Validate(new CreateRoomCommand(SiteId, new string('r', 101))).Errors.Should().ContainSingle(e => e.PropertyName == "Name");
}
