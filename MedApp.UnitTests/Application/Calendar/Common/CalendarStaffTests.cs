using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Common;

[TestFixture]
public sealed class CalendarStaffTests
{
    private Mock<IIdentityReadService> _identity = null!;
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public void SetUp()
    {
        _identity = new Mock<IIdentityReadService>();
        _identity.Setup(i => i.GetStaffAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Staff);
        _cts = new CancellationTokenSource();
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    [Test]
    public async Task GetUsernameOrThrowAsync_MemberOfStaff_ReturnsTheirUsername()
    {
        var username = await _identity.Object.GetUsernameOrThrowAsync(OtherStaffUserId, _cts.Token);

        username.Should().Be(OtherStaffUsername);
        _identity.Verify(i => i.GetStaffAsync(_cts.Token), Times.Once);
    }

    [Test]
    public async Task GetUsernameOrThrowAsync_NotAMemberOfStaff_ThrowsNotFound()
    {
        var stranger = Guid.NewGuid();

        var act = () => _identity.Object.GetUsernameOrThrowAsync(stranger, _cts.Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Staff member '{stranger}' was not found.");
    }
}
