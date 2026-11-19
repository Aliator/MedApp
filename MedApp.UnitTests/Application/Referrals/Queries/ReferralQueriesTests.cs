using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Paging;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Referrals.Queries.CountReferralsAwaitingTriage;
using MedApp.Application.Referrals.Queries.GetReferralById;
using MedApp.Application.Referrals.Queries.SearchReferrals;
using MedApp.Application.Referrals.Repositories;
using MedApp.Application.Teams.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Teams;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ReferralsTestConstants;

namespace MedApp.UnitTests.Application.Referrals.Queries;

[TestFixture]
public sealed class ReferralQueriesTests
{
    private Mock<IReferralRepository> _repository = null!;
    private Mock<ITeamRepository> _teams = null!;
    private RecordingActivityLog _activity = null!;
    private GrantsTeamBoundary _walls = null!;
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new Mock<IReferralRepository>();
        _teams = new Mock<ITeamRepository>();
        _teams.Setup(s => s.GetForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _activity = new RecordingActivityLog();
        _walls = new GrantsTeamBoundary(_activity);
        _cts = new CancellationTokenSource();
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    private void GivenMembership(params Team[] teams) =>
        _teams.Setup(s => s.GetForUserAsync(Registrar.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(teams);

    [Test]
    public async Task GetById_RecordsReferralViewedWithItsIdAndReturnsItsPage()
    {
        var referral = CreateReferral();
        _repository.Setup(r => r.GetByIdAsync(referral.Id, _cts.Token)).ReturnsAsync(referral);
        _repository.Setup(r => r.GetHistoryAsync(referral.Id, _cts.Token)).ReturnsAsync([]);
        _repository.Setup(r => r.CountLettersAsync(referral.Id, _cts.Token)).ReturnsAsync(2);

        var response = await new GetReferralByIdHandler(_repository.Object, _walls.Boundary, _activity, new FixedTimeProvider(NowUtc))
            .Handle(new GetReferralByIdQuery(referral.Id, Registrar), _cts.Token);

        response.LetterCount.Should().Be(2);
        var view = _activity.SingleViews.Single();
        view.Action.Should().Be(ActivityAction.ReferralViewed);
        view.PatientId.Should().Be(referral.PatientId);
        view.Details.Should().Be($"{{\"referralId\":\"{referral.Id}\",\"access\":\"System\"}}");
        _activity.Tokens.Should().Equal(_cts.Token);
    }

    [Test]
    public async Task GetById_Unknown_IsNotFoundAndRecordsNothing()
    {
        var act = () => new GetReferralByIdHandler(_repository.Object, _walls.Boundary, _activity, new FixedTimeProvider(NowUtc))
            .Handle(new GetReferralByIdQuery(Guid.NewGuid(), Registrar), _cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
        _activity.SingleViews.Should().BeEmpty();
    }

    private SearchReferralsQuery Search(bool mine = false, IReadOnlyList<Guid>? teamIds = null) =>
        new(1, 10, null, true, ["AwaitingTriage"], teamIds, mine, null, null, Registrar.UserId);

    private SearchReferralsHandler SearchHandler => new(_repository.Object, _teams.Object, _activity, new FixedTimeProvider(NowUtc));

    [Test]
    public async Task Search_ListsEachPatientShown()
    {
        var first = CreateReferral();
        var second = CreateReferral();
        _repository.Setup(r => r.SearchAsync(It.IsAny<SearchReferralsQuery>(), null, Today, _cts.Token))
            .ReturnsAsync(new PagedResult<ReferralRow>([new(first, true), new(second, false)], 12));

        var page = await SearchHandler.Handle(Search(), _cts.Token);

        page.TotalCount.Should().Be(12);
        page.TotalPages.Should().Be(2);
        page.Items.Select(i => i.Id).Should().Equal(first.Id, second.Id);
        page.Items[0].DaysSinceReceived.Should().Be(8);
        var listed = _activity.Views.Single();
        listed.Action.Should().Be(ActivityAction.PatientListed);
        listed.PatientIds.Should().Equal(first.PatientId, second.PatientId);
    }

    [Test]
    public async Task Search_ByPatientName_RecordsTheFiltersUsedButNeverTheTerm()
    {
        _repository.Setup(r => r.SearchAsync(It.IsAny<SearchReferralsQuery>(), null, Today, _cts.Token))
            .ReturnsAsync(new PagedResult<ReferralRow>([new(CreateReferral(), false)], 1));

        await SearchHandler.Handle(Search() with { Search = "Synthia" }, _cts.Token);

        var searched = _activity.Recorded.Single();
        searched.Action.Should().Be(ActivityAction.PatientsSearched);
        searched.Details.Should().Contain("patientName").And.Contain("statuses").And.Contain("\"resultCount\":1")
            .And.Contain("referrals").And.NotContain("Synthia");
    }

    [Test]
    public async Task Search_WithoutAPatientName_RecordsNoSearch()
    {
        _repository.Setup(r => r.SearchAsync(It.IsAny<SearchReferralsQuery>(), null, Today, _cts.Token))
            .ReturnsAsync(new PagedResult<ReferralRow>([], 0));

        await SearchHandler.Handle(Search() with { PatientId = Guid.NewGuid() }, _cts.Token);

        _activity.Recorded.Should().BeEmpty();
    }

    [Test]
    public void FiltersUsed_NamesEachFilterGiven()
    {
        ReferralSearch.FiltersUsed(new SearchReferralsQuery(1, 10, null, true, null, null, false, null, null, Registrar.UserId))
            .Should().BeEmpty();
        ReferralSearch.FiltersUsed(new SearchReferralsQuery(
                1, 10, null, true, ["Accepted"], [Guid.NewGuid()], true, Guid.NewGuid(), " x ", Registrar.UserId))
            .Should().Equal("patientName", "patientId", "statuses", "teamIds", "mine");
    }

    [Test]
    public async Task GetById_ATriagerInItsTeam_CanTriage()
    {
        var referral = CreateReferral();
        _repository.Setup(r => r.GetByIdAsync(referral.Id, _cts.Token)).ReturnsAsync(referral);
        _repository.Setup(r => r.GetHistoryAsync(referral.Id, _cts.Token)).ReturnsAsync([]);
        _repository.Setup(r => r.IsTeamMemberAsync(Triager.UserId, referral.TeamId, _cts.Token)).ReturnsAsync(true);

        var response = await new GetReferralByIdHandler(_repository.Object, _walls.Boundary, _activity, new FixedTimeProvider(NowUtc))
            .Handle(new GetReferralByIdQuery(referral.Id, Triager), _cts.Token);

        response.CanTriage.Should().BeTrue();
    }

    [Test]
    public async Task GetById_SomeoneWithoutTheTriagePermission_IsNotAskedAboutMembership()
    {
        var referral = CreateReferral();
        _repository.Setup(r => r.GetByIdAsync(referral.Id, _cts.Token)).ReturnsAsync(referral);
        _repository.Setup(r => r.GetHistoryAsync(referral.Id, _cts.Token)).ReturnsAsync([]);

        var response = await new GetReferralByIdHandler(_repository.Object, _walls.Boundary, _activity, new FixedTimeProvider(NowUtc))
            .Handle(new GetReferralByIdQuery(referral.Id, Registrar), _cts.Token);

        response.CanTriage.Should().BeFalse();
        response.WhyCantTriage.Should().Be(ReferralWorkflow.NoTriagePermissionMessage);
        _repository.Verify(r => r.IsTeamMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Search_Mine_KeepsTheViewersTeams_OrEveryOneWhenTheyHaveNone()
    {
        var mine = TeamsTestConstants.CreateTeam();
        var other = Guid.NewGuid();
        _repository.Setup(r => r.SearchAsync(It.IsAny<SearchReferralsQuery>(), It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ReferralRow>([], 0));

        await SearchHandler.Handle(Search(mine: true), _cts.Token);
        _repository.Verify(r => r.SearchAsync(It.IsAny<SearchReferralsQuery>(), null, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()));

        GivenMembership(mine);
        await SearchHandler.Handle(Search(mine: true), _cts.Token);
        _repository.Verify(r => r.SearchAsync(It.IsAny<SearchReferralsQuery>(),
            It.Is<IReadOnlyCollection<Guid>?>(ids => ids != null && ids.SequenceEqual(new[] { mine.Id })), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()));

        await SearchHandler.Handle(Search(mine: true, teamIds: [other, mine.Id]), _cts.Token);
        _repository.Verify(r => r.SearchAsync(It.IsAny<SearchReferralsQuery>(),
            It.Is<IReadOnlyCollection<Guid>?>(ids => ids != null && ids.Count == 1 && ids.Contains(mine.Id)), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

        await SearchHandler.Handle(Search(teamIds: [other]), _cts.Token);
        _repository.Verify(r => r.SearchAsync(It.IsAny<SearchReferralsQuery>(),
            It.Is<IReadOnlyCollection<Guid>?>(ids => ids != null && ids.Count == 1 && ids.Contains(other)), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task Count_OnlyWhatTheViewerMaySee_InTheirTeamsOrAnAdminsEveryTeam_RecordingNothing()
    {
        var mine = TeamsTestConstants.CreateTeam();
        var member = GrantsTeamBoundary.MemberOf(Registrar.UserId, mine.Id);
        var admin = GrantsTeamBoundary.AdminScope(Registrar.UserId);
        _repository.Setup(r => r.CountAwaitingTriageAsync(admin, _cts.Token)).ReturnsAsync(9);
        _repository.Setup(r => r.CountAwaitingTriageAsync(member, _cts.Token)).ReturnsAsync(3);
        var handler = new CountReferralsAwaitingTriageHandler(_repository.Object);

        var all = await handler.Handle(new CountReferralsAwaitingTriageQuery(Registrar.UserId, admin), _cts.Token);
        all.Count.Should().Be(9);
        all.InYourTeams.Should().BeFalse();

        var yours = await handler.Handle(new CountReferralsAwaitingTriageQuery(Registrar.UserId, member), _cts.Token);
        yours.Count.Should().Be(3);
        yours.InYourTeams.Should().BeTrue();
        _activity.Recorded.Should().BeEmpty();
        _activity.Views.Should().BeEmpty();
    }

    [Test]
    public void SearchValidator_ChecksPagingSortAndStatuses()
    {
        var validator = new SearchReferralsValidator();
        validator.Validate(Search()).IsValid.Should().BeTrue();
        validator.Validate(Search() with { Page = 0 }).IsValid.Should().BeFalse();
        validator.Validate(Search() with { PageSize = 101 }).IsValid.Should().BeFalse();
        validator.Validate(Search() with { Sort = "daysWaited" }).IsValid.Should().BeTrue();
        validator.Validate(Search() with { Sort = "waitedLongest" }).IsValid.Should().BeFalse();
        validator.Validate(Search() with { Statuses = ["Open"] }).IsValid.Should().BeFalse();
        validator.Validate(Search() with { Search = new string('a', 201) }).IsValid.Should().BeFalse();
        validator.Validate(Search() with { TeamIds = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToList() }).IsValid.Should().BeTrue();
        validator.Validate(Search() with { TeamIds = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToList() }).IsValid.Should().BeFalse();
    }
}

[TestFixture]
public sealed class ActivityLogRecordViewTests
{
    [Test]
    public async Task RecordView_SameViewInTheWindow_AddsNothing_OtherwiseAddsItWithItsDetails()
    {
        var context = new Mock<IActivityContext>();
        context.Setup(c => c.Actor).Returns(ActivityTestConstants.Actor);
        var repository = new Mock<MedApp.Application.Activity.Repositories.IActivityRepository>();
        var added = new List<ActivityEntry>();
        repository.Setup(r => r.AddAsync(It.IsAny<IReadOnlyCollection<ActivityEntry>>(), It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyCollection<ActivityEntry> entries, CancellationToken _) => added.AddRange(entries))
            .Returns(Task.CompletedTask);
        var log = new ActivityLog(context.Object, repository.Object, new FixedTimeProvider(NowUtc),
            Microsoft.Extensions.Options.Options.Create(new ActivityOptions { ViewWindowMinutes = 5 }));
        var patientId = Guid.NewGuid();
        var referralId = Guid.NewGuid();
        var details = $"{{\"referralId\":\"{referralId}\"}}";

        repository.Setup(r => r.HasRecentEntryAsync(ActivityTestConstants.Actor.UserId, ActivityAction.ReferralViewed, patientId, details,
                NowUtc.AddMinutes(-5), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        await log.RecordViewAsync(ActivityAction.ReferralViewed, patientId, new { referralId }, CancellationToken.None);
        added.Should().BeEmpty();

        await log.RecordViewAsync(ActivityAction.ReferralViewed, patientId, new { referralId = Guid.NewGuid() }, CancellationToken.None);
        added.Single().Action.Should().Be(ActivityAction.ReferralViewed);
        added.Single().PatientId.Should().Be(patientId);
    }
}
