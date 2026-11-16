using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.DayTemplates.Commands.CreateDayTemplate;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarSetupTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.DayTemplates.Commands.CreateDayTemplate;

[TestFixture]
public sealed class CreateDayTemplateHandlerTests : CalendarSetupHandlerTestBase
{
    private DayTemplate? _added;

    private CreateDayTemplateHandler Handler => Fixture.Create<CreateDayTemplateHandler>();

    [SetUp]
    public void SetUp()
    {
        _added = null;
        GivenActivityTypes(Lunch, Training);
        Repository
            .Setup(r => r.AddDayTemplateAsync(It.IsAny<DayTemplate>(), It.IsAny<CancellationToken>()))
            .Callback((DayTemplate template, CancellationToken _) =>
            {
                // As the database would: each block gets its activity type.
                foreach (var block in template.Blocks)
                    block.ActivityType = block.ActivityTypeId == Lunch.Id ? Lunch : Training;
                _added = template;
            })
            .Returns(Task.CompletedTask);
        Repository
            .Setup(r => r.GetDayTemplateAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, DateOnly _, CancellationToken _) => _added?.Id == id ? _added : null);
    }

    [Test]
    public async Task Handle_NewTemplate_AddsItsBlocksFromTodayAndReturnsItSorted()
    {
        var command = new CreateDayTemplateCommand(
            $" {DayTemplateName} ",
            [BlockValue(14, 15, Training.Id, $"  {TemplateNote} "), BlockValue(12, 13)]);

        var response = await Handler.Handle(command, Token);

        _added.Should().NotBeNull();
        _added!.Id.Should().NotBeEmpty();
        _added.Name.Should().Be(DayTemplateName);
        _added.IsArchived.Should().BeFalse();
        _added.Blocks.Should().HaveCount(2);
        _added.Blocks.Should().OnlyContain(b => b.DayTemplateId == _added.Id && b.ValidFrom == Today && b.ValidTo == null);
        _added.Blocks.Should().ContainSingle(b => b.ActivityTypeId == Training.Id && b.Note == TemplateNote);
        _added.Blocks.Should().ContainSingle(b => b.ActivityTypeId == Lunch.Id && b.Note == string.Empty);

        response.Id.Should().Be(_added.Id);
        response.Name.Should().Be(DayTemplateName);
        response.Blocks.Select(b => b.ActivityName).Should().Equal(Lunch.Name, Training.Name);
        response.Blocks[1].Note.Should().Be(TemplateNote);
        response.WeekTemplateCount.Should().Be(0);
        response.CanBeDeleted.Should().BeTrue();

        Repository.Verify(r => r.DayTemplateNameExistsAsync(DayTemplateName, null, Token), Times.Once);
        Repository.Verify(r => r.GetActivityTypesByIdAsync(It.IsAny<IEnumerable<Guid>>(), Token), Times.Once);
        Repository.Verify(r => r.AddDayTemplateAsync(_added, Token), Times.Once);
        Repository.Verify(r => r.GetDayTemplateAsync(_added.Id, Today, Token), Times.Once);
    }

    [Test]
    public async Task Handle_NoBlocks_AddsAnEmptyTemplate()
    {
        var response = await Handler.Handle(new CreateDayTemplateCommand(DayTemplateName, []), Token);

        _added!.Blocks.Should().BeEmpty();
        response.Blocks.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_NameTaken_ThrowsConflictAndAddsNothing()
    {
        Repository.Setup(r => r.DayTemplateNameExistsAsync(DayTemplateName, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => Handler.Handle(new CreateDayTemplateCommand(DayTemplateName, [BlockValue(12, 13)]), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*'{DayTemplateName}' already exists*");
        _added.Should().BeNull();
    }

    [Test]
    public async Task Handle_UnknownActivityType_ThrowsNotFoundAndAddsNothing()
    {
        var unknown = Guid.NewGuid();

        var act = () => Handler.Handle(new CreateDayTemplateCommand(DayTemplateName, [BlockValue(12, 13, unknown)]), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"*{unknown}*");
        _added.Should().BeNull();
    }

    [Test]
    public async Task Handle_ArchivedActivityType_ThrowsConflictAndAddsNothing()
    {
        var archived = CreateArchivedActivityType();
        GivenActivityTypes(archived);

        var act = () => Handler.Handle(new CreateDayTemplateCommand(DayTemplateName, [BlockValue(12, 13, archived.Id)]), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*{archived.Name}*");
        _added.Should().BeNull();
    }
}
