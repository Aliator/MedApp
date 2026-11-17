using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.WeekTemplates.Commands.CreateWeekTemplate;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarSetupTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.WeekTemplates.Commands.CreateWeekTemplate;

[TestFixture]
public sealed class CreateWeekTemplateHandlerTests : CalendarSetupHandlerTestBase
{
    private DayTemplate _clinicDay = null!;
    private DayTemplate _theatreDay = null!;
    private WeekTemplate? _added;

    private CreateWeekTemplateHandler Handler => Fixture.Create<CreateWeekTemplateHandler>();

    /// <summary><see cref="StaffUserId"/>'s clinic and theatre days.</summary>
    [SetUp]
    public void SetUp()
    {
        _added = null;
        _clinicDay = BuildDayTemplate();
        _theatreDay = BuildDayTemplate(id: OtherDayTemplateId, name: OtherDayTemplateName);
        GivenDayTemplates(_clinicDay, _theatreDay);
        Repository
            .Setup(r => r.AddWeekTemplateAsync(It.IsAny<WeekTemplate>(), It.IsAny<CancellationToken>()))
            .Callback((WeekTemplate template, CancellationToken _) =>
            {
                // As the database would: each day gets its day template.
                foreach (var day in template.Days)
                    day.DayTemplate = day.DayTemplateId == DayTemplateId ? _clinicDay : _theatreDay;
                _added = template;
            })
            .Returns(Task.CompletedTask);
        Repository
            .Setup(r => r.GetWeekTemplateAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, DateOnly _, CancellationToken _) => _added?.Id == id ? _added : null);
    }

    /// <summary>A week template for <see cref="StaffUserId"/> by default, asked for by them.</summary>
    private static CreateWeekTemplateCommand Command(
        Guid? monday = null,
        Guid? tuesday = null,
        Guid? wednesday = null,
        Guid? thursday = null,
        Guid? friday = null,
        string name = WeekTemplateName,
        Guid? ownerUserId = null,
        CalendarActor? actor = null) =>
        new(ownerUserId ?? StaffUserId, name, monday, tuesday, wednesday, thursday, friday, actor ?? StaffActor);

    [Test]
    public async Task Handle_NewTemplate_AddsTheChosenDaysFromTodayAndReturnsEveryWeekday()
    {
        var command = Command(DayTemplateId, null, OtherDayTemplateId, null, DayTemplateId, $" {WeekTemplateName} ");

        var response = await Handler.Handle(command, Token);

        _added.Should().NotBeNull();
        _added!.Id.Should().NotBeEmpty();
        _added.OwnerUserId.Should().Be(StaffUserId);
        _added.Name.Should().Be(WeekTemplateName);
        _added.Days.Select(d => d.DayOfWeek).Should().Equal(DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday);
        _added.Days.Should().OnlyContain(d => d.WeekTemplateId == _added.Id && d.ValidFrom == Today && d.ValidTo == null);

        response.Id.Should().Be(_added.Id);
        response.OwnerUserId.Should().Be(StaffUserId);
        response.Name.Should().Be(WeekTemplateName);
        response.Days.Select(d => d.DayOfWeek).Should().Equal("Monday", "Tuesday", "Wednesday", "Thursday", "Friday");
        response.Days.Select(d => d.DayTemplateId).Should().Equal(DayTemplateId, null, OtherDayTemplateId, null, DayTemplateId);
        response.Days.Select(d => d.DayTemplateName).Should().Equal(DayTemplateName, null, OtherDayTemplateName, null, DayTemplateName);
        response.StaffCount.Should().Be(0);
        response.CanBeDeleted.Should().BeTrue();
        response.CanEdit.Should().BeTrue();

        Identity.Verify(i => i.GetStaffAsync(Token), Times.Once);
        Repository.Verify(r => r.WeekTemplateNameExistsAsync(StaffUserId, WeekTemplateName, null, Token), Times.Once);
        Repository.Verify(r => r.GetDayTemplatesByIdAsync(It.IsAny<IEnumerable<Guid>>(), Token), Times.Once);
        Repository.Verify(r => r.AddWeekTemplateAsync(_added, Token), Times.Once);
        Repository.Verify(r => r.GetWeekTemplateAsync(_added.Id, Today, Token), Times.Once);
    }

    [Test]
    public async Task Handle_AdminForSomeoneElse_AddsItToTheirTemplates()
    {
        var theirDay = BuildDayTemplate(id: Guid.NewGuid(), ownerUserId: OtherStaffUserId);
        GivenDayTemplates(theirDay);

        var response = await Handler.Handle(Command(friday: theirDay.Id, ownerUserId: OtherStaffUserId, actor: AdminActor), Token);

        _added!.OwnerUserId.Should().Be(OtherStaffUserId);
        _added.Days.Should().ContainSingle(d => d.DayTemplateId == theirDay.Id);
        response.OwnerUserId.Should().Be(OtherStaffUserId);
        response.CanEdit.Should().BeTrue();
        Repository.Verify(r => r.WeekTemplateNameExistsAsync(OtherStaffUserId, WeekTemplateName, null, Token), Times.Once);
    }

    [Test]
    public async Task Handle_OtherStaffForSomeoneElse_ThrowsForbiddenAndAddsNothing()
    {
        var act = () => Handler.Handle(Command(DayTemplateId, actor: OtherStaffActor), Token);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage(CalendarActor.NotYourTemplates);
        _added.Should().BeNull();
        Identity.Verify(i => i.GetStaffAsync(It.IsAny<CancellationToken>()), Times.Never);
        Repository.Verify(r => r.AddWeekTemplateAsync(It.IsAny<WeekTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_OwnerIsNotStaff_ThrowsNotFoundAndAddsNothing()
    {
        var stranger = Guid.NewGuid();

        var act = () => Handler.Handle(Command(ownerUserId: stranger, actor: AdminActor), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"*{stranger}*");
        _added.Should().BeNull();
        Repository.Verify(
            r => r.WeekTemplateNameExistsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Handle_NoDays_AddsAnEmptyWeek()
    {
        var response = await Handler.Handle(Command(), Token);

        _added!.Days.Should().BeEmpty();
        response.Days.Should().HaveCount(5).And.OnlyContain(d => d.DayTemplateId == null);
    }

    [Test]
    public async Task Handle_NameTakenAmongOwnersTemplates_ThrowsConflictAndAddsNothing()
    {
        Repository
            .Setup(r => r.WeekTemplateNameExistsAsync(StaffUserId, WeekTemplateName, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => Handler.Handle(Command(DayTemplateId), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*'{WeekTemplateName}' already exists*");
        _added.Should().BeNull();
    }

    [Test]
    public async Task Handle_NameTakenOnlyBySomeoneElse_AddsIt()
    {
        Repository
            .Setup(r => r.WeekTemplateNameExistsAsync(OtherStaffUserId, WeekTemplateName, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Handler.Handle(Command(DayTemplateId), Token);

        _added.Should().NotBeNull();
    }

    [Test]
    public async Task Handle_UnknownDayTemplate_ThrowsNotFoundAndAddsNothing()
    {
        var unknown = Guid.NewGuid();

        var act = () => Handler.Handle(Command(tuesday: unknown), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"*{unknown}*");
        _added.Should().BeNull();
    }

    [Test]
    public async Task Handle_SomeoneElsesDayTemplate_ThrowsNotFoundAndAddsNothing()
    {
        var theirDay = BuildDayTemplate(id: Guid.NewGuid(), name: OtherDayTemplateName, ownerUserId: OtherStaffUserId);
        GivenDayTemplates(theirDay);

        var act = () => Handler.Handle(Command(DayTemplateId, theirDay.Id), Token);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"Day template(s) not found among this person's templates: {theirDay.Id}.");
        _added.Should().BeNull();
    }

    [Test]
    public async Task Handle_ArchivedDayTemplate_ThrowsConflictAndAddsNothing()
    {
        _theatreDay.IsArchived = true;

        var act = () => Handler.Handle(Command(tuesday: OtherDayTemplateId), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*{OtherDayTemplateName}*");
        _added.Should().BeNull();
    }
}
