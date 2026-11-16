using AutoFixture;
using FluentAssertions;
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

    [Test]
    public async Task Handle_NewTemplate_AddsTheChosenDaysFromTodayAndReturnsEveryWeekday()
    {
        var command = new CreateWeekTemplateCommand($" {WeekTemplateName} ", DayTemplateId, null, OtherDayTemplateId, null, DayTemplateId);

        var response = await Handler.Handle(command, Token);

        _added.Should().NotBeNull();
        _added!.Id.Should().NotBeEmpty();
        _added.Name.Should().Be(WeekTemplateName);
        _added.Days.Select(d => d.DayOfWeek).Should().Equal(DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday);
        _added.Days.Should().OnlyContain(d => d.WeekTemplateId == _added.Id && d.ValidFrom == Today && d.ValidTo == null);

        response.Id.Should().Be(_added.Id);
        response.Name.Should().Be(WeekTemplateName);
        response.Days.Select(d => d.DayOfWeek).Should().Equal("Monday", "Tuesday", "Wednesday", "Thursday", "Friday");
        response.Days.Select(d => d.DayTemplateId).Should().Equal(DayTemplateId, null, OtherDayTemplateId, null, DayTemplateId);
        response.Days.Select(d => d.DayTemplateName).Should().Equal(DayTemplateName, null, OtherDayTemplateName, null, DayTemplateName);
        response.StaffCount.Should().Be(0);
        response.CanBeDeleted.Should().BeTrue();

        Repository.Verify(r => r.WeekTemplateNameExistsAsync(WeekTemplateName, null, Token), Times.Once);
        Repository.Verify(r => r.GetDayTemplatesByIdAsync(It.IsAny<IEnumerable<Guid>>(), Token), Times.Once);
        Repository.Verify(r => r.AddWeekTemplateAsync(_added, Token), Times.Once);
        Repository.Verify(r => r.GetWeekTemplateAsync(_added.Id, Today, Token), Times.Once);
    }

    [Test]
    public async Task Handle_NoDays_AddsAnEmptyWeek()
    {
        var response = await Handler.Handle(new CreateWeekTemplateCommand(WeekTemplateName, null, null, null, null, null), Token);

        _added!.Days.Should().BeEmpty();
        response.Days.Should().HaveCount(5).And.OnlyContain(d => d.DayTemplateId == null);
    }

    [Test]
    public async Task Handle_NameTaken_ThrowsConflictAndAddsNothing()
    {
        Repository.Setup(r => r.WeekTemplateNameExistsAsync(WeekTemplateName, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => Handler.Handle(new CreateWeekTemplateCommand(WeekTemplateName, DayTemplateId, null, null, null, null), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*'{WeekTemplateName}' already exists*");
        _added.Should().BeNull();
    }

    [Test]
    public async Task Handle_UnknownDayTemplate_ThrowsNotFoundAndAddsNothing()
    {
        var unknown = Guid.NewGuid();

        var act = () => Handler.Handle(new CreateWeekTemplateCommand(WeekTemplateName, null, unknown, null, null, null), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"*{unknown}*");
        _added.Should().BeNull();
    }

    [Test]
    public async Task Handle_ArchivedDayTemplate_ThrowsConflictAndAddsNothing()
    {
        _theatreDay.IsArchived = true;

        var act = () => Handler.Handle(new CreateWeekTemplateCommand(WeekTemplateName, null, OtherDayTemplateId, null, null, null), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*{OtherDayTemplateName}*");
        _added.Should().BeNull();
    }
}
