using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.StaffCalendars.Queries.GetCalendarStaff;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Calendar;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.StaffCalendars.Queries.GetCalendarStaff;

[TestFixture]
public sealed class GetCalendarStaffHandlerTests : StaffCalendarHandlerTestBase
{
    private GetCalendarStaffHandler _handler = null!;
    private Mock<IPermissionHolders> _holders = null!;

    [SetUp]
    public void SetUp()
    {
        // Nobody has a default week, unless a test says otherwise.
        Repository
            .Setup(r => r.GetStaffWeekTemplatesOnAsync(Today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StaffWeekTemplate>());
        _holders = new Mock<IPermissionHolders>();
        Fixture.Inject(_holders.Object);
        _handler = Fixture.Create<GetCalendarStaffHandler>();
    }

    private static StaffWeekTemplate Week(Guid staffUserId, Guid weekTemplateId, DateOnly validFrom)
    {
        var week = CreateStaffWeek(weekTemplateId, staffUserId, validFrom);
        week.WeekTemplate = CreateWeekTemplate(weekTemplateId);
        week.WeekTemplate.Name = weekTemplateId == WeekTemplateId ? WeekTemplateName : "Late week";
        return week;
    }

    [Test]
    public async Task Handle_ListsEveryMemberOfStaffInOrderWithTheirDefaultWeek()
    {
        Repository
            .Setup(r => r.GetStaffWeekTemplatesOnAsync(Today, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Week(StaffUserId, WeekTemplateId, LongAgo)]);

        var response = await _handler.Handle(new GetCalendarStaffQuery(), Token);

        Identity.Verify(i => i.GetStaffAsync(Token), Times.Once);
        Repository.Verify(r => r.GetStaffWeekTemplatesOnAsync(Today, Token), Times.Once);
        response.Select(s => s.UserId).Should().Equal(StaffUserId, OtherStaffUserId, AdminUserId);
        response.Select(s => s.Username).Should().Equal(StaffUsername, OtherStaffUsername, AdminUsername);
        response[0].WeekTemplateId.Should().Be(WeekTemplateId);
        response[0].WeekTemplateName.Should().Be(WeekTemplateName);
        response[1].WeekTemplateId.Should().BeNull();
        response[1].WeekTemplateName.Should().BeNull();
    }

    [Test]
    public async Task Handle_TwoRowsForOnePerson_UsesTheLatest()
    {
        Repository
            .Setup(r => r.GetStaffWeekTemplatesOnAsync(Today, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Week(OtherStaffUserId, OtherWeekTemplateId, Today),
                Week(OtherStaffUserId, WeekTemplateId, LongAgo)
            ]);

        var response = await _handler.Handle(new GetCalendarStaffQuery(), Token);

        response.Single(s => s.UserId == OtherStaffUserId).WeekTemplateId.Should().Be(OtherWeekTemplateId);
    }

    [Test]
    public async Task Handle_NoStaff_ReturnsEmpty()
    {
        Identity.Setup(i => i.GetStaffAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<(Guid, string)>());
        Repository
            .Setup(r => r.GetStaffWeekTemplatesOnAsync(Today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StaffWeekTemplate>());

        var response = await _handler.Handle(new GetCalendarStaffQuery(), Token);

        response.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_NoPermission_ListsEveryoneWithoutAskingWhoHoldsWhat()
    {
        var response = await _handler.Handle(new GetCalendarStaffQuery(), Token);

        response.Should().HaveCount(3);
        _holders.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Handle_Permission_ListsOnlyItsHoldersInOrder()
    {
        _holders
            .Setup(h => h.WhoHoldsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), Permission.WorkPathways, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { AdminUserId, StaffUserId });

        var response = await _handler.Handle(new GetCalendarStaffQuery(nameof(Permission.WorkPathways)), Token);

        response.Select(s => s.UserId).Should().Equal(StaffUserId, AdminUserId);
        _holders.Verify(h => h.WhoHoldsAsync(
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { StaffUserId, OtherStaffUserId, AdminUserId })),
            Permission.WorkPathways,
            Token), Times.Once);
    }

    [Test]
    public async Task Handle_UnknownPermission_ListsNobody()
    {
        // The validator refuses it first; were it to get here, a name code doesn't know grants nothing.
        var response = await _handler.Handle(new GetCalendarStaffQuery("Nope"), Token);

        response.Should().BeEmpty();
        _holders.VerifyNoOtherCalls();
    }
}
