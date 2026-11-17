using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.StaffCalendars.Commands.SetStaffWeekTemplate;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.StaffCalendars.Commands.SetStaffWeekTemplate;

[TestFixture]
public sealed class SetStaffWeekTemplateHandlerTests : StaffCalendarHandlerTestBase
{
    private SetStaffWeekTemplateHandler _handler = null!;
    private TemplateHistoryChange<StaffWeekTemplate>? _saved;

    [SetUp]
    public void SetUp()
    {
        GivenWeekTemplate(CreateWeekTemplate());
        GivenRows();
        _saved = null;
        Repository
            .Setup(r => r.SaveStaffWeekTemplatesAsync(It.IsAny<TemplateHistoryChange<StaffWeekTemplate>>(), It.IsAny<CancellationToken>()))
            .Callback((TemplateHistoryChange<StaffWeekTemplate> change, CancellationToken _) => _saved = change)
            .Returns(Task.CompletedTask);
        _handler = Fixture.Create<SetStaffWeekTemplateHandler>();
    }

    private void GivenWeekTemplate(WeekTemplate template) =>
        SetupRepository
            .Setup(r => r.GetWeekTemplateAsync(template.Id, Today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

    private void GivenRows(params StaffWeekTemplate[] rows) =>
        Repository
            .Setup(r => r.GetStaffWeekTemplatesForUpdateAsync(StaffUserId, Today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

    [Test]
    public async Task Handle_NoDefaultWeek_AddsOneFromToday()
    {
        var current = CreateStaffWeek(validFrom: Today);
        current.WeekTemplate = CreateWeekTemplate();
        Repository.Setup(r => r.GetStaffWeekTemplateOnAsync(StaffUserId, Today, It.IsAny<CancellationToken>())).ReturnsAsync(current);

        var response = await _handler.Handle(new SetStaffWeekTemplateCommand(StaffUserId, WeekTemplateId, StaffActor), Token);

        VerifyLockedOnce(StaffUserId);
        SetupRepository.Verify(r => r.GetWeekTemplateAsync(WeekTemplateId, Today, Token), Times.Once);
        Repository.Verify(r => r.GetStaffWeekTemplatesForUpdateAsync(StaffUserId, Today, Token), Times.Once);
        Repository.Verify(r => r.SaveStaffWeekTemplatesAsync(It.IsAny<TemplateHistoryChange<StaffWeekTemplate>>(), Token), Times.Once);
        Repository.Verify(r => r.GetStaffWeekTemplateOnAsync(StaffUserId, Today, Token), Times.Once);
        Identity.Verify(i => i.GetStaffAsync(Token), Times.Once);

        _saved!.Delete.Should().BeEmpty();
        _saved.Close.Should().BeEmpty();
        var added = _saved.Add.Should().ContainSingle().Subject;
        added.StaffUserId.Should().Be(StaffUserId);
        added.WeekTemplateId.Should().Be(WeekTemplateId);
        added.ValidFrom.Should().Be(Today);
        added.Id.Should().NotBeEmpty();

        response.StaffUserId.Should().Be(StaffUserId);
        response.WeekTemplateId.Should().Be(WeekTemplateId);
        response.WeekTemplateName.Should().Be(WeekTemplateName);
        response.Since.Should().Be(Today);
    }

    [Test]
    public async Task Handle_AnotherDefaultWeekSinceLongAgo_ClosesItAndAddsTheNewOne()
    {
        var old = CreateStaffWeek(weekTemplateId: OtherWeekTemplateId);
        GivenRows(old);

        await _handler.Handle(new SetStaffWeekTemplateCommand(StaffUserId, WeekTemplateId, StaffActor), Token);

        _saved!.Close.Should().ContainSingle().Which.Should().BeSameAs(old);
        _saved.ClosedValidTo.Should().Be(Today.AddDays(-1));
        _saved.Add.Should().ContainSingle().Which.WeekTemplateId.Should().Be(WeekTemplateId);
    }

    [Test]
    public async Task Handle_TheSameDefaultWeek_SavesNothing()
    {
        GivenRows(CreateStaffWeek());

        var response = await _handler.Handle(new SetStaffWeekTemplateCommand(StaffUserId, WeekTemplateId, StaffActor), Token);

        Repository.Verify(r => r.SaveStaffWeekTemplatesAsync(It.IsAny<TemplateHistoryChange<StaffWeekTemplate>>(), It.IsAny<CancellationToken>()), Times.Never);
        response.StaffUserId.Should().Be(StaffUserId);
    }

    [Test]
    public async Task Handle_None_ClosesTheDefaultWeekWithoutLookingOneUp()
    {
        var old = CreateStaffWeek();
        GivenRows(old);

        var response = await _handler.Handle(new SetStaffWeekTemplateCommand(StaffUserId, null, StaffActor), Token);

        _saved!.Close.Should().ContainSingle().Which.Should().BeSameAs(old);
        _saved.Add.Should().BeEmpty();
        SetupRepository.Verify(r => r.GetWeekTemplateAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
        response.WeekTemplateId.Should().BeNull();
        response.WeekTemplateName.Should().BeNull();
        response.Since.Should().BeNull();
    }

    [Test]
    public async Task Handle_ArchivedWeekTemplateNotAlreadyTheirs_ThrowsConflict()
    {
        GivenWeekTemplate(CreateWeekTemplate(isArchived: true));

        var act = () => _handler.Handle(new SetStaffWeekTemplateCommand(StaffUserId, WeekTemplateId, StaffActor), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"The '{WeekTemplateName}' week template is archived, so it can't be chosen.");
        _saved.Should().BeNull();
    }

    [Test]
    public async Task Handle_ArchivedWeekTemplateTheyHadUntilYesterday_ThrowsConflict()
    {
        GivenWeekTemplate(CreateWeekTemplate(isArchived: true));
        GivenRows(CreateStaffWeek(validTo: Today.AddDays(-1)));

        var act = () => _handler.Handle(new SetStaffWeekTemplateCommand(StaffUserId, WeekTemplateId, StaffActor), Token);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Handle_ArchivedWeekTemplateAlreadyTheirs_KeepsIt()
    {
        GivenWeekTemplate(CreateWeekTemplate(isArchived: true));
        GivenRows(CreateStaffWeek());

        await _handler.Handle(new SetStaffWeekTemplateCommand(StaffUserId, WeekTemplateId, StaffActor), Token);

        _saved.Should().BeNull();
    }

    [Test]
    public async Task Handle_UnknownWeekTemplate_ThrowsNotFound()
    {
        var unknown = Guid.NewGuid();

        var act = () => _handler.Handle(new SetStaffWeekTemplateCommand(StaffUserId, unknown, StaffActor), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Week template '{unknown}' was not found.");
        VerifyNotLocked();
    }

    [Test]
    public async Task Handle_SomeoneElsesWeekTemplate_ThrowsNotFound()
    {
        GivenWeekTemplate(CreateWeekTemplate(ownerUserId: OtherStaffUserId));

        var act = () => _handler.Handle(new SetStaffWeekTemplateCommand(StaffUserId, WeekTemplateId, StaffActor), Token);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"Week template '{WeekTemplateId}' isn't one of this person's templates.");
        SetupRepository.Verify(r => r.GetWeekTemplateAsync(WeekTemplateId, Today, Token), Times.Once);
        VerifyNotLocked();
        _saved.Should().BeNull();
    }

    [Test]
    public async Task Handle_AdminWithTheirOwnWeekTemplateForSomeoneElse_ThrowsNotFound()
    {
        // An admin may set anyone's default week, but only to one of that person's own week templates.
        GivenWeekTemplate(CreateWeekTemplate(ownerUserId: AdminUserId));

        var act = () => _handler.Handle(new SetStaffWeekTemplateCommand(StaffUserId, WeekTemplateId, AdminActor), Token);

        await act.Should().ThrowAsync<NotFoundException>();
        VerifyNotLocked();
    }

    [Test]
    public async Task Handle_AdminForSomeoneElse_SetsIt()
    {
        await _handler.Handle(new SetStaffWeekTemplateCommand(StaffUserId, WeekTemplateId, AdminActor), Token);

        VerifyLockedOnce(StaffUserId);
        _saved!.Add.Should().ContainSingle().Which.StaffUserId.Should().Be(StaffUserId);
    }

    [Test]
    public async Task Handle_StaffForSomeoneElse_ThrowsForbidden()
    {
        var act = () => _handler.Handle(new SetStaffWeekTemplateCommand(StaffUserId, WeekTemplateId, OtherStaffActor), Token);

        await act.Should().ThrowAsync<ForbiddenException>();
        VerifyNotLocked();
    }

    [Test]
    public async Task Handle_NotAMemberOfStaff_ThrowsNotFound()
    {
        var act = () => _handler.Handle(new SetStaffWeekTemplateCommand(Guid.NewGuid(), WeekTemplateId, AdminActor), Token);

        await act.Should().ThrowAsync<NotFoundException>();
        VerifyNotLocked();
    }
}
