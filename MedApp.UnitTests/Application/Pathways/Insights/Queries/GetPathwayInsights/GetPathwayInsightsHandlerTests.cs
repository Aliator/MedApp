using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Insights.Common;
using MedApp.Application.Pathways.Insights.Queries.GetPathwayInsights;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Specialties.Repositories;
using MedApp.Domain.Specialties;
using MedApp.Contracts.Pathways.Insights.Responses;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.InsightsTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Pathways.Insights.Queries.GetPathwayInsights;

[TestFixture]
public sealed class GetPathwayInsightsHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IPathwayInsightsRepository> _repository = null!;
    private Mock<IPatientPathwayStagesRepository> _stages = null!;
    private Mock<IIdentityReadService> _identity = null!;
    private Mock<ISpecialtyRepository> _specialties = null!;

    private static readonly InsightsScope TemplateScope = new(TemplateId);

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _repository = new Mock<IPathwayInsightsRepository>();
        _stages = new Mock<IPatientPathwayStagesRepository>();
        _identity = new Mock<IIdentityReadService>();
        _specialties = new Mock<ISpecialtyRepository>();

        _repository.Setup(r => r.GetStatusCountsAsync(It.IsAny<InsightsScope>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _repository.Setup(r => r.GetStatusCountsAtAsync(It.IsAny<InsightsScope>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _repository.Setup(r => r.CountOverdueByAssigneeAsync(It.IsAny<InsightsScope>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(([], 0));
        _repository.Setup(r => r.CountOverdueByPriorityAsync(It.IsAny<InsightsScope>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _repository.Setup(r => r.CountCreationsByHourAsync(It.IsAny<InsightsScope>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _repository.Setup(r => r.CountCompletionsByHourAsync(It.IsAny<InsightsScope>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _repository.Setup(r => r.GetStageDurationsAsync(It.IsAny<InsightsScope>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _identity.Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                (IReadOnlyDictionary<Guid, string>)Usernames.Where(kv => ids.Contains(kv.Key)).ToDictionary());
        _stages.Setup(s => s.GetPathwayTemplateByIdAsync(TemplateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTemplate());
        _specialties.Setup(s => s.GetAsync(SpecialtyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Specialty { Id = SpecialtyId, Name = "Dermatology" });

        _fixture.Inject(_repository.Object);
        _fixture.Inject(_stages.Object);
        _fixture.Inject(_identity.Object);
        _fixture.Inject(_specialties.Object);
        _fixture.Inject<TimeProvider>(new FixedTimeProvider(Now));
    }

    private Task<PathwayInsightsResponse> Handle(GetPathwayInsightsQuery query, CancellationToken ct = default) =>
        _fixture.Create<GetPathwayInsightsHandler>().Handle(query, ct);

    [Test]
    public async Task Handle_Query_AsksForEachTotalOverTheRightMoments()
    {
        await Handle(new GetPathwayInsightsQuery(4, TemplateId));

        var start = ThisWeekStartUtc.AddDays(-21);
        _repository.Verify(r => r.GetStatusCountsAsync(TemplateScope, Now, It.IsAny<CancellationToken>()));
        _repository.Verify(r => r.GetStatusCountsAtAsync(TemplateScope, Now.AddDays(-7), It.IsAny<CancellationToken>()));
        _repository.Verify(r => r.CountCompletionsAsync(TemplateScope, ThisWeekStartUtc, Now, It.IsAny<CancellationToken>()));
        _repository.Verify(r => r.CountCompletionsAsync(TemplateScope, LastWeekStartUtc, Now.AddDays(-7), It.IsAny<CancellationToken>()));
        _repository.Verify(r => r.CountOverdueByAssigneeAsync(TemplateScope, Now, It.IsAny<CancellationToken>()));
        _repository.Verify(r => r.CountOverdueByPriorityAsync(TemplateScope, Now, It.IsAny<CancellationToken>()));
        _repository.Verify(r => r.CountCreationsByHourAsync(TemplateScope, start, Now, It.IsAny<CancellationToken>()));
        _repository.Verify(r => r.CountCompletionsByHourAsync(TemplateScope, start, Now, It.IsAny<CancellationToken>()));
        _repository.Verify(r => r.GetStageDurationsAsync(TemplateScope, start, Now, It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task Handle_Totals_MapsThemWithAssigneesNames()
    {
        _repository.Setup(r => r.GetStatusCountsAsync(InsightsScope.Everything, Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Count(Status.InProgress, 3, isPastDue: true)]);
        _repository.Setup(r => r.CountCompletionsAsync(InsightsScope.Everything, ThisWeekStartUtc, Now, It.IsAny<CancellationToken>())).ReturnsAsync(2);
        _repository.Setup(r => r.CountOverdueByAssigneeAsync(InsightsScope.Everything, Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(([new AssigneeCount(Staff.UserId, 3)], 1));

        var response = await Handle(new GetPathwayInsightsQuery(12, null));

        response.Weeks.Should().Be(12);
        response.TemplateId.Should().BeNull();
        response.Overdue.Current.Should().Be(3);
        response.InProgress.Current.Should().Be(3);
        response.CompletedThisWeek.Current.Should().Be(2);
        response.OverdueByAssignee.Select(a => (a.Username, a.Count)).Should().Equal((Staff.Username, 3), (null, 1));
        response.Throughput.Should().HaveCount(12);
        _stages.Verify(s => s.GetPathwayTemplateByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_UnknownTemplate_ThrowsNotFound()
    {
        var unknown = Guid.NewGuid();
        _stages.Setup(s => s.GetPathwayTemplateByIdAsync(unknown, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PathwayTemplate?)null);

        var act = () => Handle(new GetPathwayInsightsQuery(4, unknown));

        await act.Should().ThrowAsync<NotFoundException>();
        _repository.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Handle_PassesCancellationToken_ToEveryCall()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        await Handle(new GetPathwayInsightsQuery(4, TemplateId), token);

        _stages.Verify(s => s.GetPathwayTemplateByIdAsync(TemplateId, token));
        _repository.Verify(r => r.GetStatusCountsAsync(TemplateScope, It.IsAny<DateTime>(), token));
        _repository.Verify(r => r.GetStatusCountsAtAsync(TemplateScope, It.IsAny<DateTime>(), token));
        _repository.Verify(r => r.CountCompletionsAsync(TemplateScope, It.IsAny<DateTime>(), It.IsAny<DateTime>(), token), Times.Exactly(2));
        _repository.Verify(r => r.CountOverdueByAssigneeAsync(TemplateScope, It.IsAny<DateTime>(), token));
        _repository.Verify(r => r.CountOverdueByPriorityAsync(TemplateScope, It.IsAny<DateTime>(), token));
        _repository.Verify(r => r.CountCreationsByHourAsync(TemplateScope, It.IsAny<DateTime>(), It.IsAny<DateTime>(), token));
        _repository.Verify(r => r.CountCompletionsByHourAsync(TemplateScope, It.IsAny<DateTime>(), It.IsAny<DateTime>(), token));
        _repository.Verify(r => r.GetStageDurationsAsync(TemplateScope, It.IsAny<DateTime>(), It.IsAny<DateTime>(), token));
        _identity.Verify(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), token));
    }

    [Test]
    public async Task Handle_Specialty_PassesCancellationTokenToItsLookUp()
    {
        using var cts = new CancellationTokenSource();

        await Handle(new GetPathwayInsightsQuery(4, null, SpecialtyId), cts.Token);

        _specialties.Verify(s => s.GetAsync(SpecialtyId, cts.Token));
    }

    [Test]
    public async Task Handle_Specialty_AsksForEachTotalInItAndSaysSo()
    {
        var response = await Handle(new GetPathwayInsightsQuery(4, TemplateId, SpecialtyId));

        var scope = new InsightsScope(TemplateId, SpecialtyId);
        _repository.Verify(r => r.GetStatusCountsAsync(scope, Now, It.IsAny<CancellationToken>()));
        _repository.Verify(r => r.GetStatusCountsAtAsync(scope, Now.AddDays(-7), It.IsAny<CancellationToken>()));
        _repository.Verify(r => r.CountCompletionsAsync(scope, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _repository.Verify(r => r.CountOverdueByAssigneeAsync(scope, Now, It.IsAny<CancellationToken>()));
        _repository.Verify(r => r.CountOverdueByPriorityAsync(scope, Now, It.IsAny<CancellationToken>()));
        _repository.Verify(r => r.CountCreationsByHourAsync(scope, It.IsAny<DateTime>(), Now, It.IsAny<CancellationToken>()));
        _repository.Verify(r => r.CountCompletionsByHourAsync(scope, It.IsAny<DateTime>(), Now, It.IsAny<CancellationToken>()));
        _repository.Verify(r => r.GetStageDurationsAsync(scope, It.IsAny<DateTime>(), Now, It.IsAny<CancellationToken>()));
        response.SpecialtyId.Should().Be(SpecialtyId);
        response.TemplateId.Should().Be(TemplateId);
    }

    [Test]
    public async Task Handle_UnknownSpecialty_ThrowsNotFound()
    {
        var unknown = Guid.NewGuid();

        var act = () => Handle(new GetPathwayInsightsQuery(4, null, unknown));

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Specialty '{unknown}' was not found.");
        _repository.VerifyNoOtherCalls();
    }
}
