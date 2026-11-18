using FluentAssertions;
using MedApp.Application.Authentication.Users.Commands.SetStaffDetails;
using MedApp.Application.Authentication.Users.Common;
using MedApp.Application.Authentication.Users.Queries.GetStaffDetails;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Domain.Activity;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.SpecialtiesTestConstants;

namespace MedApp.UnitTests.Application.Auth.Users.Commands.SetStaffDetails;

/// <summary>Staff full names and job titles: setting, reading and validating them, and what the activity log gets.</summary>
[TestFixture]
public sealed class SetStaffDetailsHandlerTests
{
    private const string FullName = "Sam Synthetic";
    private const string JobTitle = "Synthetic nurse specialist";

    private Mock<IIdentityReadService> _users = null!;
    private Mock<IStaffDetailsService> _details = null!;
    private RecordingActivityLog _activityLog = null!;
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public void SetUp()
    {
        _users = new Mock<IIdentityReadService>();
        _users.Setup(u => u.GetUserAsync(StaffUsername, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserResponse(StaffUserId, StaffUsername, []));
        _details = new Mock<IStaffDetailsService>();
        _details.Setup(d => d.GetAsync(StaffUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StaffDetails(FullName, null));
        _details.Setup(d => d.SetAsync(StaffUserId, It.IsAny<StaffDetails>(), It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _activityLog = new RecordingActivityLog();
        _cts = new CancellationTokenSource();
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    private SetStaffDetailsHandler Handler => new(_users.Object, _details.Object, _activityLog);

    [Test]
    public async Task Handle_Changed_SavesThemTidiedWithAnEntryNamingTheFieldsOnly()
    {
        ActivityEntry? entry = null;
        StaffDetails? saved = null;
        _details.Setup(d => d.SetAsync(StaffUserId, It.IsAny<StaffDetails>(), It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()))
            .Callback((Guid _, StaffDetails d, ActivityEntry e, CancellationToken _) => (saved, entry) = (d, e))
            .ReturnsAsync(true);

        var response = await Handler.Handle(
            new SetStaffDetailsCommand(StaffUsername, "  Dr   Sam  Synthetic ", $" {JobTitle} "), _cts.Token);

        saved.Should().Be(new StaffDetails("Dr Sam Synthetic", JobTitle));
        response.Should().BeEquivalentTo(new StaffDetailsResponse { FullName = "Dr Sam Synthetic", JobTitle = JobTitle });
        entry!.Action.Should().Be(ActivityAction.StaffDetailsChanged);
        entry.TargetUsername.Should().Be(StaffUsername);
        entry.Details.Should().Be("""{"fields":["fullName","jobTitle"]}""");
        entry.Details.Should().NotContain("Synthetic");
        _details.Verify(d => d.SetAsync(StaffUserId, It.IsAny<StaffDetails>(), It.IsAny<ActivityEntry>(), _cts.Token));
    }

    [Test]
    public async Task Handle_EmptyRemovesOne()
    {
        ActivityEntry? entry = null;
        _details.Setup(d => d.SetAsync(StaffUserId, new StaffDetails(null, null), It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()))
            .Callback((Guid _, StaffDetails _, ActivityEntry e, CancellationToken _) => entry = e)
            .ReturnsAsync(true);

        var response = await Handler.Handle(new SetStaffDetailsCommand(StaffUsername, " ", null), _cts.Token);

        response.FullName.Should().BeNull();
        entry!.Details.Should().Be("""{"fields":["fullName"]}""");
    }

    [Test]
    public async Task Handle_Unchanged_SavesAndRecordsNothing()
    {
        var response = await Handler.Handle(new SetStaffDetailsCommand(StaffUsername, FullName, ""), _cts.Token);

        response.FullName.Should().Be(FullName);
        _details.Verify(d => d.SetAsync(It.IsAny<Guid>(), It.IsAny<StaffDetails>(), It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_UnknownAccount_IsNotFound()
    {
        var act = () => Handler.Handle(new SetStaffDetailsCommand("nobody", FullName, null), _cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_AccountGoneBeforeTheRead_IsNotFound()
    {
        _details.Setup(d => d.GetAsync(StaffUserId, It.IsAny<CancellationToken>())).ReturnsAsync((StaffDetails?)null);

        var act = () => Handler.Handle(new SetStaffDetailsCommand(StaffUsername, JobTitle, null), _cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_AccountGoneBeforeTheSave_IsNotFound()
    {
        _details.Setup(d => d.SetAsync(StaffUserId, It.IsAny<StaffDetails>(), It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = () => Handler.Handle(new SetStaffDetailsCommand(StaffUsername, JobTitle, null), _cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task GetStaffDetails_ReturnsThem_AndUnknownIsNotFound()
    {
        var handler = new GetStaffDetailsHandler(_users.Object, _details.Object);

        (await handler.Handle(new GetStaffDetailsQuery(StaffUsername), _cts.Token))
            .Should().BeEquivalentTo(new StaffDetailsResponse { FullName = FullName });
        _details.Verify(d => d.GetAsync(StaffUserId, _cts.Token));

        var unknown = () => handler.Handle(new GetStaffDetailsQuery("nobody"), _cts.Token);
        await unknown.Should().ThrowAsync<NotFoundException>();

        _details.Setup(d => d.GetAsync(StaffUserId, It.IsAny<CancellationToken>())).ReturnsAsync((StaffDetails?)null);
        var gone = () => handler.Handle(new GetStaffDetailsQuery(StaffUsername), _cts.Token);
        await gone.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public void Validator_LimitsEachToAHundredCharactersOnceTidied()
    {
        var validator = new SetStaffDetailsValidator();

        validator.Validate(new SetStaffDetailsCommand(StaffUsername, new string('a', 100) + "   ", null)).IsValid.Should().BeTrue();
        validator.Validate(new SetStaffDetailsCommand(StaffUsername, null, null)).IsValid.Should().BeTrue();
        validator.Validate(new SetStaffDetailsCommand("", new string('a', 101), new string('b', 101)))
            .Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(["Username", "FullName", "JobTitle"]);
    }

    [Test]
    public void Mapper_GivesBoth() =>
        StaffDetailsMapper.ToResponse(new StaffDetails(FullName, JobTitle))
            .Should().BeEquivalentTo(new StaffDetailsResponse { FullName = FullName, JobTitle = JobTitle });
}
