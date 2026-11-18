using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Paging;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Pathways.PatientPathways.Queries.SearchPatientPathways;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Pathways.Timeline.Queries.GetPatientTimeline;
using MedApp.Application.Pathways.Timeline.Queries.GetPatientTimelineOverview;
using MedApp.Domain.Activity;
using MedApp.Domain.Patients;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;
using TimelineConstants = MedApp.UnitTests.Common.Constants.TimelineTestConstants;

namespace MedApp.UnitTests.Application.Pathways;

/// <summary>
/// What reading pathways records in the activity log: a pathway search records each patient on its page as
/// listed, and a patient's timeline and its overview record a view of the patient.
/// </summary>
[TestFixture]
public sealed class PathwayReadActivityTests : PathwayHandlerTestBase
{
    private RecordingActivityLog _activity = null!;

    [SetUp]
    public void SetUpActivity()
    {
        _activity = new RecordingActivityLog();
        Fixture.Inject<IActivityLog>(_activity);
    }

    [Test]
    public async Task SearchPathways_RecordsEachPatientOnThePageAsListed()
    {
        var first = CreatePathway(Status.Assigned);
        var second = CreatePathway(Status.InProgress);
        var query = CreateSearchQuery();
        Repository.Setup(r => r.SearchAsync(query, Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<PatientPathway>([first, second], 2));
        using var cts = new CancellationTokenSource();

        await Fixture.Create<SearchPatientPathwaysHandler>().Handle(query, cts.Token);

        var (action, ids, pathwayId) = _activity.Views.Should().ContainSingle().Subject;
        action.Should().Be(ActivityAction.PatientListed);
        ids.Should().Equal(first.PatientId, second.PatientId);
        pathwayId.Should().BeNull();
        _activity.Tokens.Should().Equal(cts.Token);
    }

    [Test]
    public async Task SearchPathways_NoResults_RecordsAnEmptyList()
    {
        var query = CreateSearchQuery();
        Repository.Setup(r => r.SearchAsync(query, Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<PatientPathway>([], 0));

        await Fixture.Create<SearchPatientPathwaysHandler>().Handle(query, CancellationToken.None);

        _activity.Views.Should().ContainSingle().Which.PatientIds.Should().BeEmpty();
    }

    private (Mock<IPatientRepository> Patients, Mock<IPathwayAuditRepository> Audit) GivenTimelinePatient()
    {
        var patients = new Mock<IPatientRepository>();
        patients.Setup(r => r.GetByIdAsync(TimelineConstants.PatientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Patient { Id = TimelineConstants.PatientId });
        var audit = new Mock<IPathwayAuditRepository>();
        audit.Setup(r => r.GetPatientTimelineAsync(It.IsAny<GetPatientTimelineQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        audit.Setup(r => r.GetDeletionsAsync(TimelineConstants.PatientId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        Appointments.Setup(r => r.GetPatientTimelineAsync(It.IsAny<GetPatientTimelineQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        Repository.Setup(r => r.GetByPatientAsync(TimelineConstants.PatientId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        Fixture.Inject(patients.Object);
        Fixture.Inject(audit.Object);
        return (patients, audit);
    }

    [Test]
    public async Task Timeline_RecordsAViewOfThePatient()
    {
        GivenTimelinePatient();
        using var cts = new CancellationTokenSource();

        await Fixture.Create<GetPatientTimelineHandler>().Handle(TimelineConstants.CreateTimelineQuery(), cts.Token);

        _activity.Views.Should().ContainSingle().Which.Should().BeEquivalentTo(
            (ActivityAction.PatientViewed, new List<Guid> { TimelineConstants.PatientId }, (Guid?)null));
        _activity.Tokens.Should().Equal(cts.Token);
    }

    [Test]
    public async Task TimelineOverview_RecordsAViewOfThePatient()
    {
        GivenTimelinePatient();

        await Fixture.Create<GetPatientTimelineOverviewHandler>().Handle(
            new GetPatientTimelineOverviewQuery(TimelineConstants.PatientId, false), CancellationToken.None);

        _activity.Views.Should().ContainSingle().Which.Action.Should().Be(ActivityAction.PatientViewed);
    }

    [Test]
    public async Task Timeline_UnknownPatient_RecordsNothing()
    {
        GivenTimelinePatient();

        var act = () => Fixture.Create<GetPatientTimelineHandler>().Handle(
            TimelineConstants.CreateTimelineQuery() with { PatientId = TimelineConstants.OtherPatientId }, CancellationToken.None);
        var overview = () => Fixture.Create<GetPatientTimelineOverviewHandler>().Handle(
            new GetPatientTimelineOverviewQuery(TimelineConstants.OtherPatientId, false), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        await overview.Should().ThrowAsync<NotFoundException>();
        _activity.Views.Should().BeEmpty();
    }
}
