using System.Text.Json;
using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Authentication.Users.Commands.SetUserSpecialties;
using MedApp.Application.Authentication.Users.Queries.GetMySpecialties;
using MedApp.Application.Authentication.Users.Queries.GetUserSpecialties;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Specialties.Repositories;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Contracts.Specialties.Responses;
using MedApp.Domain.Activity;
using MedApp.Domain.Specialties;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.SpecialtiesTestConstants;

namespace MedApp.UnitTests.Application.Auth.Users.Commands.SetUserSpecialties;

[TestFixture]
public sealed class SetUserSpecialtiesHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IIdentityReadService> _users = null!;
    private Mock<ISpecialtyRepository> _repository = null!;
    private RecordingActivityLog _activityLog = null!;
    private Specialty _dermatology = null!;
    private Specialty _urology = null!;
    private Specialty _archived = null!;
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _dermatology = CreateSpecialty();
        _urology = CreateSpecialty(OtherSpecialtyName);
        _archived = CreateSpecialty(ThirdSpecialtyName, isArchived: true);
        _repository = RepositoryWith(_dermatology, _urology, _archived);
        _users = new Mock<IIdentityReadService>();
        _users.Setup(u => u.GetUserAsync(StaffUsername, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserResponse(StaffUserId, StaffUsername, []));
        _activityLog = new RecordingActivityLog();
        _fixture.Inject(_repository.Object);
        _fixture.Inject(_users.Object);
        _fixture.Inject<IActivityLog>(_activityLog);
        _cts = new CancellationTokenSource();
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    private void GivenTheyBelongTo(params Specialty[] specialties) =>
        _repository.Setup(r => r.GetForUserAsync(StaffUserId, It.IsAny<CancellationToken>())).ReturnsAsync(specialties);

    private Task<IReadOnlyList<StaffSpecialtyResponse>> Set(params Guid[] ids) =>
        _fixture.Create<SetUserSpecialtiesHandler>().Handle(new SetUserSpecialtiesCommand(StaffUsername, ids), _cts.Token);

    [Test]
    public async Task Handle_NewSet_SavesItWithTheActivityEntryAndReturnsItSorted()
    {
        GivenTheyBelongTo(_dermatology);
        ActivityEntry? saved = null;
        IReadOnlyCollection<Guid>? savedIds = null;
        _repository
            .Setup(r => r.SetForUserAsync(StaffUserId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()))
            .Callback((Guid _, IReadOnlyCollection<Guid> ids, ActivityEntry entry, CancellationToken _) => (savedIds, saved) = (ids, entry));

        var response = await Set(_urology.Id, _dermatology.Id);

        response.Select(s => s.Name).Should().Equal(SpecialtyName, OtherSpecialtyName);
        savedIds.Should().BeEquivalentTo([_dermatology.Id, _urology.Id]);
        saved!.Action.Should().Be(ActivityAction.StaffSpecialtiesChanged);
        saved.TargetUsername.Should().Be(StaffUsername);
        var details = JsonDocument.Parse(saved.Details).RootElement;
        details.GetProperty("from").EnumerateArray().Select(e => e.GetString()).Should().Equal(SpecialtyName);
        details.GetProperty("to").EnumerateArray().Select(e => e.GetString()).Should().Equal(SpecialtyName, OtherSpecialtyName);
        details.GetProperty("toIds").EnumerateArray().Select(e => e.GetGuid()).Should().Equal(_dermatology.Id, _urology.Id);
        details.GetProperty("fromIds").EnumerateArray().Select(e => e.GetGuid()).Should().Equal(_dermatology.Id);
        _repository.Verify(r => r.SetForUserAsync(StaffUserId, It.IsAny<IReadOnlyCollection<Guid>>(), saved, _cts.Token));
    }

    [Test]
    public async Task Handle_Empty_RemovesThemAll()
    {
        GivenTheyBelongTo(_dermatology, _urology);

        var response = await Set();

        response.Should().BeEmpty();
        _repository.Verify(r => r.SetForUserAsync(
            StaffUserId, It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 0), It.IsAny<ActivityEntry>(), _cts.Token));
    }

    [Test]
    public async Task Handle_SameSet_SavesAndRecordsNothing()
    {
        GivenTheyBelongTo(_dermatology);

        var response = await Set(_dermatology.Id, _dermatology.Id);

        response.Should().ContainSingle(s => s.Id == _dermatology.Id);
        _repository.Verify(r => r.SetForUserAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_KeepingAnArchivedOne_IsAllowed()
    {
        GivenTheyBelongTo(_archived);

        var response = await Set(_archived.Id, _urology.Id);

        response.Select(s => (s.Name, s.IsArchived)).Should().Equal((ThirdSpecialtyName, true), (OtherSpecialtyName, false));
    }

    [Test]
    public async Task Handle_NewlyGivingAnArchivedOne_ThrowsConflictWithoutSaving()
    {
        GivenTheyBelongTo();

        var act = () => Set(_archived.Id);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*{ThirdSpecialtyName}*");
        _repository.Verify(r => r.SetForUserAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_UnknownSpecialty_ThrowsNotFound()
    {
        GivenTheyBelongTo();

        var act = () => Set(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_UnknownUser_ThrowsNotFound()
    {
        var act = () => _fixture.Create<SetUserSpecialtiesHandler>()
            .Handle(new SetUserSpecialtiesCommand("nobody", [_dermatology.Id]), _cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task GetUserSpecialties_ReturnsTheirsSorted_OrNotFound()
    {
        GivenTheyBelongTo(_urology, _dermatology);
        var handler = _fixture.Create<GetUserSpecialtiesHandler>();

        (await handler.Handle(new GetUserSpecialtiesQuery(StaffUsername), _cts.Token))
            .Select(s => s.Name).Should().Equal(SpecialtyName, OtherSpecialtyName);
        var act = () => handler.Handle(new GetUserSpecialtiesQuery("nobody"), _cts.Token);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task GetMySpecialties_ReturnsTheSignedInUsers()
    {
        GivenTheyBelongTo(_urology);

        var response = await _fixture.Create<GetMySpecialtiesHandler>().Handle(new GetMySpecialtiesQuery(StaffUserId), _cts.Token);

        response.Should().ContainSingle(s => s.Id == _urology.Id);
        _repository.Verify(r => r.GetForUserAsync(StaffUserId, _cts.Token));
    }
}
