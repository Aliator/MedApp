using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Queries.SearchRetentionReview;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Activity;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.Extensions.Options;
using Moq;

namespace MedApp.UnitTests.Application.Patients.Queries.SearchRetentionReview;

[TestFixture]
public sealed class SearchRetentionReviewHandlerTests
{
    // ActivityTestConstants.Now is 16 November 2026 (GMT), so today in the UK is 16 November 2026.
    private static readonly DateOnly Today = new(2026, 11, 16);
    private static readonly DateOnly Adult = new(1960, 1, 1);

    private IFixture _fixture = null!;
    private Mock<IPatientRepository> _repository = null!;
    private RecordingActivityLog _activity = null!;
    private List<RetentionCandidate> _candidates = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _repository = new Mock<IPatientRepository>();
        _candidates = [];
        _repository
            .Setup(r => r.GetRetentionCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _candidates);
        _activity = new RecordingActivityLog();
        _fixture.Inject(_repository.Object);
        _fixture.Inject<IActivityLog>(_activity);
        _fixture.Inject<TimeProvider>(new FixedTimeProvider(ActivityTestConstants.Now));
        _fixture.Inject(Options.Create(new RetentionOptions()));
    }

    private SearchRetentionReviewHandler Handler => _fixture.Create<SearchRetentionReviewHandler>();

    /// <summary>A candidate whose last change was at midday UTC on <paramref name="day"/>.</summary>
    private RetentionCandidate Add(DateOnly day, DateOnly? dateOfBirth = null, DateOnly? keptUntil = null, string lastName = "Patient", string firstName = "Test")
    {
        var candidate = PrivacyTestConstants.CreateCandidate(
            dateOfBirth ?? Adult, day.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc), keptUntil: keptUntil,
            firstName: firstName, lastName: lastName);
        _candidates.Add(candidate);
        return candidate;
    }

    [Test]
    public async Task Handle_AsksForThoseChangedBeforeTheShortestPeriod()
    {
        using var cts = new CancellationTokenSource();

        await Handler.Handle(new SearchRetentionReviewQuery(1, 10, false), cts.Token);

        // The end of 16 November 2018 in the UK: eight years (the shortest period) before today.
        var expected = DueDates.EndOfDayUtc(new DateOnly(2018, 11, 16));
        expected.Date.Should().Be(new DateTime(2018, 11, 16));
        _repository.Verify(r => r.GetRetentionCandidatesAsync(expected, cts.Token), Times.Once);
    }

    [Test]
    public async Task Handle_ListsOnlyThoseDue_LongestOverdueFirst_ThenByName()
    {
        var dueToday = Add(Today.AddYears(-8));
        Add(Today.AddYears(-8).AddDays(1)); // due tomorrow
        Add(new DateOnly(2010, 1, 1), dateOfBirth: new DateOnly(2005, 1, 1)); // a child: due at 25, in 2030
        var longOverdueB = Add(new DateOnly(2010, 1, 1), lastName: "Bravo");
        var longOverdueA = Add(new DateOnly(2010, 1, 1), lastName: "alpha");

        var response = await Handler.Handle(new SearchRetentionReviewQuery(1, 10, false), CancellationToken.None);

        response.Items.Select(i => i.PatientId).Should().Equal(longOverdueA.PatientId, longOverdueB.PatientId, dueToday.PatientId);
        response.TotalCount.Should().Be(3);
        response.Items[0].RetentionClass.Should().Be("Adult");
        response.Items[0].DueOn.Should().Be(new DateOnly(2018, 1, 1));
    }

    [Test]
    public async Task Handle_Kept_IsHiddenUntilItsDate_UnlessAskedFor()
    {
        var keptForNow = Add(new DateOnly(2010, 1, 1), keptUntil: Today.AddDays(1));
        var keepingEnded = Add(new DateOnly(2010, 1, 1), keptUntil: Today, firstName: "Zed");

        var hidden = await Handler.Handle(new SearchRetentionReviewQuery(1, 10, false), CancellationToken.None);
        var shown = await Handler.Handle(new SearchRetentionReviewQuery(1, 10, true), CancellationToken.None);

        hidden.Items.Select(i => i.PatientId).Should().Equal(keepingEnded.PatientId);
        shown.Items.Select(i => i.PatientId).Should().BeEquivalentTo([keptForNow.PatientId, keepingEnded.PatientId]);
        shown.Items.Single(i => i.PatientId == keptForNow.PatientId).KeptUntil.Should().Be(Today.AddDays(1));
    }

    [Test]
    public async Task Handle_Pages_AndRecordsOnlyThePagesPatientsAsSeen()
    {
        var all = Enumerable.Range(0, 5).Select(i => Add(new DateOnly(2010, 1, 1).AddDays(i))).ToList();
        using var cts = new CancellationTokenSource();

        var response = await Handler.Handle(new SearchRetentionReviewQuery(2, 2, false), cts.Token);

        response.Items.Select(i => i.PatientId).Should().Equal(all[2].PatientId, all[3].PatientId);
        response.TotalCount.Should().Be(5);
        response.TotalPages.Should().Be(3);
        response.Page.Should().Be(2);
        var view = _activity.Views.Should().ContainSingle().Subject;
        view.Action.Should().Be(ActivityAction.PatientListed);
        view.PatientIds.Should().Equal(all[2].PatientId, all[3].PatientId);
        _activity.Tokens.Should().Equal(cts.Token);
    }
}
