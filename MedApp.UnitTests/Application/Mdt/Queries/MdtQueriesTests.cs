using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Mdt.Common;
using MedApp.Application.Mdt.Queries.GetMdtItemsAwaitingMeeting;
using MedApp.Application.Mdt.Queries.GetMdtMeetingList;
using MedApp.Application.Mdt.Queries.GetMdtMeetings;
using MedApp.Application.Mdt.Queries.GetPathwayMdtItems;
using MedApp.Application.Mdt.Repositories;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Activity;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.MdtTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Mdt.Queries;

/// <summary>Every read of an MDT list names its patients in the activity log; the MDTs themselves name none.</summary>
[TestFixture]
public sealed class MdtQueriesTests
{
    private Mock<IMdtItemRepository> _repository = null!;
    private Mock<IIdentityReadService> _identity = null!;
    private RecordingActivityLog _activity = null!;
    private readonly FixedTimeProvider _clock = new(Now);
    private readonly CancellationToken _token = new CancellationTokenSource().Token;

    [SetUp]
    public void SetUp()
    {
        _repository = new Mock<IMdtItemRepository>();
        _identity = new Mock<IIdentityReadService>();
        _identity
            .Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [OtherUserId] = OtherUsername, [Staff.UserId] = Staff.Username });
        _activity = new RecordingActivityLog();
    }

    [Test]
    public async Task GetMeetingList_RecordsPatientListedForEachPatient_InListedOrder()
    {
        var meeting = CreateMdt(Now.AddDays(1));
        var first = CreateItem(meeting);
        var second = CreateItem(meeting);
        _repository.Setup(r => r.GetMeetingRowAsync(meeting.Id, It.IsAny<TeamScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MdtMeetingRow(meeting, Team.Name, 2, 2));
        _repository.Setup(r => r.GetForMeetingAsync(meeting.Id, It.IsAny<TeamScope>(), It.IsAny<CancellationToken>())).ReturnsAsync([first, second]);

        var response = await new GetMdtMeetingListHandler(_repository.Object, _identity.Object, _activity, _clock)
            .Handle(new GetMdtMeetingListQuery(meeting.Id, Clinician), _token);

        response.Items.Select(i => i.Id).Should().Equal(first.Id, second.Id);
        response.Meeting.TeamName.Should().Be(Team.Name);
        response.Meeting.Attendees.Select(a => a.Username).Should().Equal(OtherUsername, Staff.Username);
        var view = _activity.Views.Should().ContainSingle().Subject;
        view.Action.Should().Be(ActivityAction.PatientListed);
        view.PatientIds.Should().BeEquivalentTo([first.PatientId, second.PatientId]);
    }

    [Test]
    public async Task GetMeetingList_NotAnMdt_IsNotFound()
    {
        await FluentActions.Awaiting(() => new GetMdtMeetingListHandler(_repository.Object, _identity.Object, _activity, _clock)
                .Handle(new GetMdtMeetingListQuery(Guid.NewGuid(), Clinician), _token))
            .Should().ThrowAsync<NotFoundException>();
        _activity.Views.Should().BeEmpty();
    }

    [Test]
    public async Task GetAwaitingMeeting_RecordsPatientListed()
    {
        var item = CreateItem(null);
        item.MeetingCancelledStartUtc = Now.AddDays(-1);
        _repository.Setup(r => r.GetAwaitingMeetingAsync(Team.Id, It.IsAny<TeamScope>(), It.IsAny<CancellationToken>())).ReturnsAsync([item]);

        var response = await new GetMdtItemsAwaitingMeetingHandler(_repository.Object, _activity, _clock)
            .Handle(new GetMdtItemsAwaitingMeetingQuery(Team.Id, Clinician), _token);

        response.Should().ContainSingle().Which.MeetingCancelledStartUtc.Should().Be(Now.AddDays(-1));
        _activity.Views.Should().ContainSingle().Which.Action.Should().Be(ActivityAction.PatientListed);
    }

    [Test]
    public async Task GetPathwayItems_RecordsPathwayViewed()
    {
        var item = CreateItem(CreateMdt(Now.AddDays(1)));
        var pathways = new Mock<IPatientPathwayRepository>();
        pathways.Setup(p => p.GetByIdAsync(item.PathwayId, It.IsAny<CancellationToken>())).ReturnsAsync(item.Pathway);
        _repository.Setup(r => r.GetForPathwayAsync(item.PathwayId, It.IsAny<CancellationToken>())).ReturnsAsync([item]);

        var response = await new GetPathwayMdtItemsHandler(_repository.Object, pathways.Object, new TeamWallsTestBoundary().Boundary, _activity, _clock)
            .Handle(new GetPathwayMdtItemsQuery(item.PathwayId, Clinician), _token);

        response.Should().ContainSingle();
        var view = _activity.Views.Should().ContainSingle().Subject;
        view.Action.Should().Be(ActivityAction.PathwayViewed);
        view.PathwayId.Should().Be(item.PathwayId);
    }

    [Test]
    public async Task GetMeetings_ReadsTheWindowAndRecordsNothing()
    {
        var meeting = CreateMdt(Now.AddDays(1));
        _repository
            .Setup(r => r.GetMeetingsAsync(null, Now.AddDays(-GetMdtMeetingsHandler.DaysBack), Now.AddDays(GetMdtMeetingsHandler.DaysAhead), TeamScope.System, _token))
            .ReturnsAsync([new MdtMeetingRow(meeting, Team.Name, 1, 3)]);

        var response = await new GetMdtMeetingsHandler(_repository.Object, _identity.Object, _clock)
            .Handle(new GetMdtMeetingsQuery(null), _token);

        response.Should().ContainSingle().Which.ItemCount.Should().Be(3);
        _activity.Views.Should().BeEmpty();
    }
}
