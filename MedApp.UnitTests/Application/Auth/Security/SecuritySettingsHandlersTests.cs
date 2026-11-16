using FluentAssertions;
using MedApp.Application.Authentication.Security.Commands.SaveSecuritySettings;
using MedApp.Application.Authentication.Security.Common;
using MedApp.Application.Authentication.Security.Queries.GetSecuritySettings;
using MedApp.Application.Authentication.Security.Repositories;
using MedApp.Application.Authentication.Sessions.Queries.GetAuthFeatures;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Domain.Authentication;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.EmailTestConstants;

namespace MedApp.UnitTests.Application.Auth.Security;

[TestFixture]
public sealed class SecuritySettingsHandlersTests
{
    private Mock<ISecuritySettingsRepository> _settings = null!;
    private Mock<IIdentityReadService> _read = null!;
    private Mock<ITwoFactorService> _twoFactor = null!;
    private Mock<ISecurityEventLog> _events = null!;
    private SecuritySettings _stored = null!;

    [SetUp]
    public void SetUp()
    {
        _stored = new SecuritySettings();
        _settings = new Mock<ISecuritySettingsRepository>();
        _settings.Setup(s => s.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _stored);
        _settings.Setup(s => s.SaveAsync(It.IsAny<SecuritySettings>(), It.IsAny<CancellationToken>()))
            .Callback<SecuritySettings, CancellationToken>((s, _) => _stored = s).Returns(Task.CompletedTask);
        _read = new Mock<IIdentityReadService>();
        _read.Setup(r => r.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [ActorId] = "an.admin" });
        _twoFactor = new Mock<ITwoFactorService>();
        _twoFactor.Setup(t => t.GetAdminsWithoutTwoFactorAsync(It.IsAny<CancellationToken>())).ReturnsAsync(["admin.one", "admin.two"]);
        _events = new Mock<ISecurityEventLog>();
    }

    private SaveSecuritySettingsHandler SaveHandler() =>
        new(_settings.Object, _read.Object, _twoFactor.Object, _events.Object, new FixedTimeProvider(NowUtc));

    [Test]
    public async Task Get_NeverSaved_IsOffWithNobody()
    {
        var result = await new GetSecuritySettingsHandler(_settings.Object, _read.Object, _twoFactor.Object)
            .Handle(new GetSecuritySettingsQuery(), CancellationToken.None);

        result.Should().BeEquivalentTo(new SecuritySettingsResponse(false, 2, null, null, ["admin.one", "admin.two"]));
        _read.Verify(r => r.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Save_TurningOnWithTwoFactor_SavesWhoAndWhenAndRecordsIt()
    {
        _twoFactor.Setup(t => t.IsEnabledAsync(ActorId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await SaveHandler().Handle(new SaveSecuritySettingsCommand(true, ActorId, "an.admin"), CancellationToken.None);

        result.Should().BeEquivalentTo(new SecuritySettingsResponse(true, 2, NowUtc, "an.admin", ["admin.one", "admin.two"]));
        _stored.UpdatedByUserId.Should().Be(ActorId);
        _events.Verify(e => e.RecordAsync(
            It.Is<SecurityEvent>(ev => ev.Kind == SecurityEventKind.TwoFactorRequirementChanged && (bool)ev.Details!["requireTwoFactorForAdmins"]!),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Save_TurningOnWithoutTwoFactorYourself_IsAConflictAndSavesNothing()
    {
        var act = () => SaveHandler().Handle(new SaveSecuritySettingsCommand(true, ActorId, "an.admin"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _settings.Verify(s => s.SaveAsync(It.IsAny<SecuritySettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Save_TurningOff_NeedsNoTwoFactorAndRecordsIt()
    {
        _stored = new SecuritySettings { RequireTwoFactorForAdmins = true };

        var result = await SaveHandler().Handle(new SaveSecuritySettingsCommand(false, ActorId, "an.admin"), CancellationToken.None);

        result.RequireTwoFactorForAdmins.Should().BeFalse();
        _events.Verify(e => e.RecordAsync(It.IsAny<SecurityEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Save_Unchanged_RecordsNoEvent()
    {
        await SaveHandler().Handle(new SaveSecuritySettingsCommand(false, ActorId, "an.admin"), CancellationToken.None);

        _events.VerifyNoOtherCalls();
    }

    [Test]
    public void Mapper_CarriesEverything() =>
        SecuritySettingsMapper.ToResponse(new SecuritySettings { RequireTwoFactorForAdmins = true, UpdatedAtUtc = NowUtc }, "someone", ["a", "b", "c"])
            .Should().BeEquivalentTo(new SecuritySettingsResponse(true, 3, NowUtc, "someone", ["a", "b", "c"]));

    [TestCase(true)]
    [TestCase(false)]
    public async Task GetAuthFeatures_SaysWhetherEmailCanBeSent(bool canSend)
    {
        var result = await new GetAuthFeaturesHandler(Availability(canSend)).Handle(new GetAuthFeaturesQuery(), CancellationToken.None);

        result.Should().Be(new AuthFeaturesResponse(canSend));
    }
}
