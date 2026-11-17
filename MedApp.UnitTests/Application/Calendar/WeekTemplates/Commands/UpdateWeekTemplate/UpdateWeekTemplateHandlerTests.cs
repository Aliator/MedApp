using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Calendar.WeekTemplates.Commands.UpdateWeekTemplate;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarSetupTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.WeekTemplates.Commands.UpdateWeekTemplate;

[TestFixture]
public sealed class UpdateWeekTemplateHandlerTests : CalendarSetupHandlerTestBase
{
    private DayTemplate _clinicDay = null!;
    private DayTemplate _theatreDay = null!;
    private WeekTemplateDay _monday = null!;
    private WeekTemplate _template = null!;
    private TemplateHistoryChange<WeekTemplateDay>? _savedChange;
    private int _saves;

    private UpdateWeekTemplateHandler Handler => Fixture.Create<UpdateWeekTemplateHandler>();

    [SetUp]
    public void SetUp()
    {
        _clinicDay = BuildDayTemplate();
        _theatreDay = BuildDayTemplate(id: OtherDayTemplateId, name: OtherDayTemplateName);
        GivenDayTemplates(_clinicDay, _theatreDay);
        _monday = CreateLoadedWeekDay(DayOfWeek.Monday, _clinicDay);
        _template = BuildWeekTemplate([_monday]);
        _savedChange = null;
        _saves = 0;

        Repository.Setup(r => r.GetWeekTemplateForUpdateAsync(WeekTemplateId, Today, It.IsAny<CancellationToken>())).ReturnsAsync(_template);
        Repository.Setup(r => r.GetWeekTemplateAsync(WeekTemplateId, Today, It.IsAny<CancellationToken>())).ReturnsAsync(_template);
        Repository
            .Setup(r => r.SaveWeekTemplateAsync(_template, It.IsAny<TemplateHistoryChange<WeekTemplateDay>?>(), It.IsAny<CancellationToken>()))
            .Callback((WeekTemplate _, TemplateHistoryChange<WeekTemplateDay>? change, CancellationToken _) =>
            {
                _savedChange = change;
                _saves++;
            })
            .Returns(Task.CompletedTask);
    }

    /// <summary>A change to <see cref="StaffUserId"/>'s standard week, by them unless another actor is given.</summary>
    private static UpdateWeekTemplateCommand Command(string name, Guid? monday, Guid? tuesday = null, CalendarActor? actor = null) =>
        new(WeekTemplateId, name, monday, tuesday, null, null, null, actor ?? StaffActor);

    [Test]
    public async Task Handle_SameDays_SavesWithNoChangeToTheDays()
    {
        var response = await Handler.Handle(Command(WeekTemplateName, DayTemplateId), Token);

        _saves.Should().Be(1);
        _savedChange.Should().BeNull();
        Repository.Verify(r => r.SaveWeekTemplateAsync(_template, null, Token), Times.Once);
        response.Days[0].DayTemplateName.Should().Be(DayTemplateName);
    }

    [Test]
    public async Task Handle_DayChanged_ClosesTheOldRowYesterdayAndAddsOneFromToday()
    {
        await Handler.Handle(Command(WeekTemplateName, OtherDayTemplateId, DayTemplateId), Token);

        _savedChange.Should().NotBeNull();
        _savedChange!.Close.Should().Equal(_monday);
        _savedChange.Delete.Should().BeEmpty();
        _savedChange.ClosedValidTo.Should().Be(Today.AddDays(-1));
        _savedChange.Add.Select(d => (d.DayOfWeek, d.DayTemplateId)).Should().BeEquivalentTo(
            [(DayOfWeek.Monday, OtherDayTemplateId), (DayOfWeek.Tuesday, DayTemplateId)]);
        _savedChange.Add.Should().OnlyContain(d => d.WeekTemplateId == WeekTemplateId && d.ValidFrom == Today);
    }

    [Test]
    public async Task Handle_DayCleared_ClosesItsRow()
    {
        await Handler.Handle(Command(WeekTemplateName, null), Token);

        _savedChange!.Close.Should().Equal(_monday);
        _savedChange.Add.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_DayAddedTodayChangedAgain_IsReplacedNotClosed()
    {
        var tuesday = CreateLoadedWeekDay(DayOfWeek.Tuesday, _clinicDay, validFrom: Today);
        _template.Days.Add(tuesday);

        await Handler.Handle(Command(WeekTemplateName, DayTemplateId, OtherDayTemplateId), Token);

        _savedChange!.Delete.Should().Equal(tuesday);
        _savedChange.Close.Should().BeEmpty();
        _savedChange.Add.Should().ContainSingle(d => d.DayOfWeek == DayOfWeek.Tuesday && d.DayTemplateId == OtherDayTemplateId);
    }

    [Test]
    public async Task Handle_Renamed_ChecksTheNameAndSavesIt()
    {
        await Handler.Handle(Command($" {OtherWeekTemplateName} ", DayTemplateId), Token);

        _template.Name.Should().Be(OtherWeekTemplateName);
        Repository.Verify(r => r.WeekTemplateNameExistsAsync(StaffUserId, OtherWeekTemplateName, WeekTemplateId, Token), Times.Once);
    }

    [Test]
    public async Task Handle_AdminChangesSomeoneElses_ChecksTheNameAmongTheOwnersTemplates()
    {
        await Handler.Handle(Command(OtherWeekTemplateName, OtherDayTemplateId, actor: AdminActor), Token);

        _template.Name.Should().Be(OtherWeekTemplateName);
        _saves.Should().Be(1);
        Repository.Verify(r => r.WeekTemplateNameExistsAsync(StaffUserId, OtherWeekTemplateName, WeekTemplateId, Token), Times.Once);
    }

    [Test]
    public async Task Handle_AdminGivesItTheirOwnDayTemplate_ThrowsNotFoundAndSavesNothing()
    {
        // The admin's own day template isn't one of the week's owner's.
        var adminsDay = BuildDayTemplate(id: Guid.NewGuid(), ownerUserId: AdminUserId);
        GivenDayTemplates(adminsDay);

        var act = () => Handler.Handle(Command(WeekTemplateName, adminsDay.Id, actor: AdminActor), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"*among this person's templates*{adminsDay.Id}*");
        _saves.Should().Be(0);
    }

    [Test]
    public async Task Handle_OtherStaffChangesSomeoneElses_ThrowsForbiddenAndSavesNothing()
    {
        var act = () => Handler.Handle(Command(OtherWeekTemplateName, OtherDayTemplateId, actor: OtherStaffActor), Token);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage(CalendarActor.NotYourTemplates);
        _template.Name.Should().Be(WeekTemplateName);
        _saves.Should().Be(0);
    }

    [Test]
    public async Task Handle_SomeoneElsesDayTemplate_ThrowsNotFoundAndSavesNothing()
    {
        var theirDay = BuildDayTemplate(id: Guid.NewGuid(), ownerUserId: OtherStaffUserId);
        GivenDayTemplates(theirDay);

        var act = () => Handler.Handle(Command(WeekTemplateName, DayTemplateId, theirDay.Id), Token);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"Day template(s) not found among this person's templates: {theirDay.Id}.");
        _saves.Should().Be(0);
    }

    [Test]
    public async Task Handle_SameName_DoesNotCheckItIsTaken()
    {
        await Handler.Handle(Command(WeekTemplateName, DayTemplateId), Token);

        Repository.Verify(r => r.WeekTemplateNameExistsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_NameTaken_ThrowsConflictAndSavesNothing()
    {
        Repository.Setup(r => r.WeekTemplateNameExistsAsync(StaffUserId, OtherWeekTemplateName, WeekTemplateId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => Handler.Handle(Command(OtherWeekTemplateName, DayTemplateId), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*'{OtherWeekTemplateName}' already exists*");
        _saves.Should().Be(0);
    }

    [Test]
    public async Task Handle_ArchivedDayTemplateAlreadyInTheWeek_KeepsIt()
    {
        _clinicDay.IsArchived = true;

        await Handler.Handle(Command(WeekTemplateName, DayTemplateId, DayTemplateId), Token);

        _saves.Should().Be(1);
        _savedChange!.Add.Should().ContainSingle(d => d.DayOfWeek == DayOfWeek.Tuesday);
    }

    [Test]
    public async Task Handle_ArchivedDayTemplateNewToTheWeek_ThrowsConflictAndSavesNothing()
    {
        _theatreDay.IsArchived = true;

        var act = () => Handler.Handle(Command(WeekTemplateName, DayTemplateId, OtherDayTemplateId), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*{OtherDayTemplateName}*");
        _saves.Should().Be(0);
    }

    [Test]
    public async Task Handle_UnknownDayTemplate_ThrowsNotFound()
    {
        var act = () => Handler.Handle(Command(WeekTemplateName, Guid.NewGuid()), Token);

        await act.Should().ThrowAsync<NotFoundException>();
        _saves.Should().Be(0);
    }

    [Test]
    public async Task Handle_TemplateDoesNotExist_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new UpdateWeekTemplateCommand(Guid.NewGuid(), WeekTemplateName, null, null, null, null, null, StaffActor), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("Week template*was not found.");
    }

    [Test]
    public async Task Handle_Saved_ReturnsTheTemplateAsSavedWithItsUsage()
    {
        GivenWeekTemplateUsage(WeekTemplateId, new WeekTemplateUsage(5, true));

        var response = await Handler.Handle(Command(WeekTemplateName, DayTemplateId), Token);

        response.Id.Should().Be(WeekTemplateId);
        response.StaffCount.Should().Be(5);
        response.CanBeDeleted.Should().BeFalse();
        response.OwnerUserId.Should().Be(StaffUserId);
        response.CanEdit.Should().BeTrue();
        Repository.Verify(r => r.GetWeekTemplateAsync(WeekTemplateId, Today, Token), Times.Once);
        Repository.Verify(r => r.GetWeekTemplateUsageAsync(It.Is<IEnumerable<Guid>>(ids => ids.Single() == WeekTemplateId), Today, Token), Times.Once);
        Repository.Verify(r => r.GetDayTemplatesByIdAsync(It.IsAny<IEnumerable<Guid>>(), Token), Times.Once);
    }
}
