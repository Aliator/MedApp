using FluentAssertions;
using MedApp.Application.Sites.Commands.UpdateRoom;
using static MedApp.UnitTests.Common.Constants.SitesTestConstants;

namespace MedApp.UnitTests.Application.Sites.Commands.UpdateRoom;

[TestFixture]
public sealed class UpdateRoomValidatorTests
{
    private readonly UpdateRoomValidator _validator = new();

    [Test]
    public void Validate_RoomAndName_IsValid() =>
        _validator.Validate(new UpdateRoomCommand(RoomId, RoomName)).IsValid.Should().BeTrue();

    [Test]
    public void Validate_NoRoom_IsInvalid() =>
        _validator.Validate(new UpdateRoomCommand(Guid.Empty, RoomName)).Errors.Should().ContainSingle(e => e.PropertyName == "RoomId");

    [Test]
    public void Validate_NoName_IsInvalid() =>
        _validator.Validate(new UpdateRoomCommand(RoomId, "")).Errors.Should().ContainSingle(e => e.PropertyName == "Name");
}
