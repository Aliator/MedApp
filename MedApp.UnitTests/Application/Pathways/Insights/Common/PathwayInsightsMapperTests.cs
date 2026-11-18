using FluentAssertions;
using MedApp.Application.Pathways.Insights.Common;
using MedApp.Contracts.Pathways.Insights.Responses;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.UnitTests.Common.Constants.InsightsTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Pathways.Insights.Common;

[TestFixture]
public sealed class PathwayInsightsMapperTests
{
    private static readonly InsightsPeriod Period = InsightsPeriod.For(Now, 3);
    private static readonly Dictionary<Guid, string> NoUsernames = [];

    private static PathwayInsightsResponse Map(InsightsData data, IReadOnlyDictionary<Guid, string>? usernames = null) =>
        PathwayInsightsMapper.ToResponse(data, Period, TemplateId, usernames ?? NoUsernames);

    [Test]
    public void ToResponse_Period_GivesTheWeeksAndTheirDates()
    {
        var response = Map(EmptyData());

        response.Weeks.Should().Be(3);
        response.TemplateId.Should().Be(TemplateId);
        response.From.Should().Be(ThisMonday.AddDays(-14));
        response.To.Should().Be(Today);
    }

    [Test]
    public void ToResponse_StatusCounts_CountsOpenInProgressAndOverdueNowAndLastWeek()
    {
        var data = EmptyData() with
        {
            StatusesNow =
            [
                Count(Status.Unassigned, 2), Count(Status.InProgress, 3), Count(Status.InProgress, 4, isPastDue: true),
                Count(Status.Blocked, 1, isPastDue: true), Count(Status.Completed, 7), Count(Status.Archived, 2, isPastDue: true),
            ],
            StatusesLastWeek = [Count(Status.InReview, 5, isPastDue: true), Count(Status.Cancelled, 1)],
            CompletedThisWeek = 6,
            CompletedLastWeek = 9,
        };

        var response = Map(data);

        // Open is everything not closed, blocked included; overdue leaves blocked out.
        response.Open.Should().BeEquivalentTo(new InsightsComparisonResponse { Current = 10, LastWeek = 5 });
        response.InProgress.Should().BeEquivalentTo(new InsightsComparisonResponse { Current = 7, LastWeek = 0 });
        response.Overdue.Should().BeEquivalentTo(new InsightsComparisonResponse { Current = 4, LastWeek = 5 });
        response.CompletedThisWeek.Should().BeEquivalentTo(new InsightsComparisonResponse { Current = 6, LastWeek = 9 });
    }

    [Test]
    public void ToResponse_OverdueByAssignee_MostFirstWithNamesAndNobodyAsItsOwnRow()
    {
        var deletedUser = Guid.NewGuid();
        var data = EmptyData() with
        {
            OverdueByAssignee = [new AssigneeCount(Staff.UserId, 2), new AssigneeCount(OtherUserId, 5), new AssigneeCount(deletedUser, 2)],
            OverdueWithNobodyAssigned = 2,
        };

        var response = Map(data, Usernames);

        response.OverdueByAssignee.Should().BeEquivalentTo(
            [
                new OverdueByAssigneeResponse { UserId = OtherUserId, Username = OtherUsername, Count = 5 },
                new OverdueByAssigneeResponse { UserId = deletedUser, Username = null, Count = 2 },
                new OverdueByAssigneeResponse { UserId = Staff.UserId, Username = Staff.Username, Count = 2 },
                new OverdueByAssigneeResponse { UserId = null, Username = null, Count = 2 },
            ],
            options => options.WithStrictOrdering());
    }

    [Test]
    public void ToResponse_NobodyUnassigned_HasNoNobodyRow()
    {
        Map(EmptyData()).OverdueByAssignee.Should().BeEmpty();
    }

    [Test]
    public void ToResponse_OverdueByPriority_ListsEveryPriorityMostUrgentFirst()
    {
        var data = EmptyData() with { OverdueByPriority = [new PriorityCount(PatientPathwayPriority.High, 3)] };

        Map(data).OverdueByPriority.Should().BeEquivalentTo(
            [
                new OverdueByPriorityResponse { Priority = "Urgent", Count = 0 },
                new OverdueByPriorityResponse { Priority = "High", Count = 3 },
                new OverdueByPriorityResponse { Priority = "Medium", Count = 0 },
                new OverdueByPriorityResponse { Priority = "Low", Count = 0 },
            ],
            options => options.WithStrictOrdering());
    }

    [Test]
    public void ToResponse_StageDurations_GroupsByTemplateByNameWithStagesInOrderInHours()
    {
        var data = EmptyData() with
        {
            StageDurations =
            [
                new StageDurationRow(TemplateId, TemplateName, 2, PathwayDefinitionId, PathwayStage, 3, 5400, 900),
                new StageDurationRow(OtherTemplateId, "A template", 1, ConsultationDefinitionId, ConsultationStage, 1, 0, 36000),
                new StageDurationRow(TemplateId, TemplateName, 1, ConsultationDefinitionId, ConsultationStage, 4, 7200, 1000),
            ],
        };

        var stageTimes = Map(data).StageTimes;

        stageTimes.Select(t => t.TemplateName).Should().Equal("A template", TemplateName);
        stageTimes[1].TemplateId.Should().Be(TemplateId);
        stageTimes[1].Stages.Should().BeEquivalentTo(
            [
                new StageTimeResponse { StageOrder = 1, StageName = ConsultationStage, FinishedCount = 4, AverageWaitingHours = 2, AverageWorkingHours = 0.28 },
                new StageTimeResponse { StageOrder = 2, StageName = PathwayStage, FinishedCount = 3, AverageWaitingHours = 1.5, AverageWorkingHours = 0.25 },
            ],
            options => options.WithStrictOrdering());
    }

    [Test]
    public void ToResponse_Throughput_SumsEachUkWeeksHours()
    {
        // Sunday 23:30 UTC is Monday 00:30 BST: this week, not last.
        var data = EmptyData() with
        {
            CreatedByHour =
            [
                new HourCount(ThisWeekStartUtc.Date, ThisWeekStartUtc.Hour, 2),
                new HourCount(ThisWeekStartUtc.Date, ThisWeekStartUtc.Hour - 1, 1),
                new HourCount(Period.StartUtc.Date, Period.StartUtc.Hour, 4),
            ],
            CompletedByHour = [new HourCount(Now.Date, Now.Hour, 3)],
        };

        Map(data).Throughput.Should().BeEquivalentTo(
            [
                new WeeklyThroughputResponse { WeekStart = ThisMonday.AddDays(-14), Created = 4, Completed = 0 },
                new WeeklyThroughputResponse { WeekStart = ThisMonday.AddDays(-7), Created = 1, Completed = 0 },
                new WeeklyThroughputResponse { WeekStart = ThisMonday, Created = 2, Completed = 3 },
            ],
            options => options.WithStrictOrdering());
    }
}
