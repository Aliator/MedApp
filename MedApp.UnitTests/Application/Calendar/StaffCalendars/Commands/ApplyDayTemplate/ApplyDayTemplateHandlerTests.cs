using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.StaffCalendars.Commands.ApplyDayTemplate;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.StaffCalendars.Commands.ApplyDayTemplate;

[TestFixture]
public sealed class ApplyDayTemplateHandlerTests : StaffCalendarHandlerTestBase
{
    private ApplyDayTemplateHandler _handler = null!;
    private CalendarDayOverride? _added;

    [SetUp]
    public void SetUp()
    {
        GivenDayTemplate(CreateDayTemplate(OtherDayTemplateId));
        // The day as read back after the change: the other day template, overridden.
        GivenCalendar(CreateData(NextMonday, overrides: [CreateOverride(NextMonday, OtherDayTemplateId)]));
        _added = null;
        Repository
            .Setup(r => r.AddDayOverrideAsync(It.IsAny<CalendarDayOverride>(), It.IsAny<CancellationToken>()))
            .Callback((CalendarDayOverride dayOverride, CancellationToken _) => _added = dayOverride)
            .Returns(Task.CompletedTask);
        _handler = Fixture.Create<ApplyDayTemplateHandler>();
    }

    private void GivenDayTemplate(DayTemplate template) =>
        SetupRepository
            .Setup(r => r.GetDayTemplateAsync(template.Id, Today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

    private CalendarDayOverride GivenExistingOverride(Guid? dayTemplateId)
    {
        var existing = CreateOverride(NextMonday, dayTemplateId);
        Repository
            .Setup(r => r.GetDayOverrideForUpdateAsync(StaffUserId, NextMonday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        return existing;
    }

    [Test]
    public async Task Handle_DayWithoutAnOverride_AddsOneHoldingTheLock()
    {
        var response = await _handler.Handle(new ApplyDayTemplateCommand(StaffUserId, NextMonday, OtherDayTemplateId, StaffActor), Token);

        VerifyLockedOnce(StaffUserId);
        SetupRepository.Verify(r => r.GetDayTemplateAsync(OtherDayTemplateId, Today, Token), Times.Once);
        Repository.Verify(r => r.GetDayOverrideForUpdateAsync(StaffUserId, NextMonday, Token), Times.Once);
        Repository.Verify(r => r.AddDayOverrideAsync(It.IsAny<CalendarDayOverride>(), Token), Times.Once);
        Repository.Verify(r => r.GetCalendarDataAsync(StaffUserId, NextMonday, NextMonday, Token), Times.Once);
        Identity.Verify(i => i.GetStaffAsync(Token), Times.Once);

        _added!.StaffUserId.Should().Be(StaffUserId);
        _added.Date.Should().Be(NextMonday);
        _added.DayTemplateId.Should().Be(OtherDayTemplateId);
        _added.Id.Should().NotBeEmpty();

        response.Date.Should().Be(NextMonday);
        response.DayTemplateId.Should().Be(OtherDayTemplateId);
        response.DayTemplateName.Should().Be(OtherDayTemplateName);
        response.IsDayTemplateOverridden.Should().BeTrue();
        response.IsWorkingDay.Should().BeTrue();
        response.IsPast.Should().BeFalse();
    }

    [Test]
    public async Task Handle_NoDayTemplate_AddsAnEmptyDayWithoutLookingOneUp()
    {
        await _handler.Handle(new ApplyDayTemplateCommand(StaffUserId, NextMonday, null, StaffActor), Token);

        _added!.DayTemplateId.Should().BeNull();
        SetupRepository.Verify(r => r.GetDayTemplateAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_DayWithAnotherOverride_ChangesIt()
    {
        var existing = GivenExistingOverride(DayTemplateId);

        await _handler.Handle(new ApplyDayTemplateCommand(StaffUserId, NextMonday, OtherDayTemplateId, StaffActor), Token);

        existing.DayTemplateId.Should().Be(OtherDayTemplateId);
        Repository.Verify(r => r.SaveAsync(Token), Times.Once);
        _added.Should().BeNull();
    }

    [Test]
    public async Task Handle_DayWithTheSameOverride_ChangesNothing()
    {
        GivenExistingOverride(OtherDayTemplateId);

        await _handler.Handle(new ApplyDayTemplateCommand(StaffUserId, NextMonday, OtherDayTemplateId, StaffActor), Token);

        Repository.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
        _added.Should().BeNull();
    }

    [Test]
    public async Task Handle_Today_IsAllowed()
    {
        GivenCalendar(CreateData(Today));

        var response = await _handler.Handle(new ApplyDayTemplateCommand(StaffUserId, Today, OtherDayTemplateId, StaffActor), Token);

        response.IsToday.Should().BeTrue();
        _added.Should().NotBeNull();
    }

    [Test]
    public async Task Handle_AdminOnSomeoneElsesCalendar_AddsTheOverride()
    {
        await _handler.Handle(new ApplyDayTemplateCommand(StaffUserId, NextMonday, OtherDayTemplateId, AdminActor), Token);

        VerifyLockedOnce(StaffUserId);
        _added.Should().NotBeNull();
    }

    [Test]
    public async Task Handle_StaffOnSomeoneElsesCalendar_ThrowsForbidden()
    {
        var act = () => _handler.Handle(new ApplyDayTemplateCommand(StaffUserId, NextMonday, OtherDayTemplateId, OtherStaffActor), Token);

        await act.Should().ThrowAsync<ForbiddenException>();
        VerifyNotLocked();
    }

    [Test]
    public async Task Handle_NotAMemberOfStaff_ThrowsNotFound()
    {
        var stranger = Guid.NewGuid();

        var act = () => _handler.Handle(new ApplyDayTemplateCommand(stranger, NextMonday, OtherDayTemplateId, AdminActor), Token);

        await act.Should().ThrowAsync<NotFoundException>();
        VerifyNotLocked();
    }

    [Test]
    public async Task Handle_PastDay_ThrowsConflict()
    {
        var act = () => _handler.Handle(new ApplyDayTemplateCommand(StaffUserId, Today.AddDays(-3), OtherDayTemplateId, StaffActor), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(CalendarGuards.PastDay);
        VerifyNotLocked();
    }

    [Test]
    public async Task Handle_UnknownDayTemplate_ThrowsNotFound()
    {
        var unknown = Guid.NewGuid();

        var act = () => _handler.Handle(new ApplyDayTemplateCommand(StaffUserId, NextMonday, unknown, StaffActor), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Day template '{unknown}' was not found.");
        VerifyNotLocked();
    }

    [Test]
    public async Task Handle_SomeoneElsesDayTemplate_ThrowsNotFound()
    {
        GivenDayTemplate(CreateDayTemplate(OtherDayTemplateId, ownerUserId: OtherStaffUserId));

        var act = () => _handler.Handle(new ApplyDayTemplateCommand(StaffUserId, NextMonday, OtherDayTemplateId, StaffActor), Token);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"Day template '{OtherDayTemplateId}' isn't one of this person's templates.");
        SetupRepository.Verify(r => r.GetDayTemplateAsync(OtherDayTemplateId, Today, Token), Times.Once);
        VerifyNotLocked();
        _added.Should().BeNull();
    }

    [Test]
    public async Task Handle_AdminWithTheirOwnDayTemplateOnSomeoneElsesCalendar_ThrowsNotFound()
    {
        // An admin may change anyone's calendar, but only with that person's own day templates.
        GivenDayTemplate(CreateDayTemplate(OtherDayTemplateId, ownerUserId: AdminUserId));

        var act = () => _handler.Handle(new ApplyDayTemplateCommand(StaffUserId, NextMonday, OtherDayTemplateId, AdminActor), Token);

        await act.Should().ThrowAsync<NotFoundException>();
        VerifyNotLocked();
    }

    [Test]
    public async Task Handle_ArchivedDayTemplate_ThrowsConflict()
    {
        GivenDayTemplate(CreateDayTemplate(OtherDayTemplateId, isArchived: true));

        var act = () => _handler.Handle(new ApplyDayTemplateCommand(StaffUserId, NextMonday, OtherDayTemplateId, StaffActor), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"The '{DayTemplateName}' day template is archived, so it can't be chosen.");
        VerifyNotLocked();
    }
}
