using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
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

    /// <summary>A day template for <see cref="StaffUserId"/> by default, asked for by them.</summary>
    private static CreateDayTemplateCommand Command(
        string name = DayTemplateName,
        IReadOnlyList<DayTemplateBlockValue>? blocks = null,
        Guid? ownerUserId = null,
        CalendarActor? actor = null) =>
        new(ownerUserId ?? StaffUserId, name, blocks ?? [BlockValue(12, 13)], actor ?? StaffActor);

    [Test]
    public async Task Handle_NewTemplate_AddsItsBlocksFromTodayAndReturnsItSorted()
    {
        var command = Command(
            $" {DayTemplateName} ",
            [BlockValue(14, 15, Training.Id, $"  {TemplateNote} "), BlockValue(12, 13)]);

        var response = await Handler.Handle(command, Token);

        _added.Should().NotBeNull();
        _added!.Id.Should().NotBeEmpty();
        _added.OwnerUserId.Should().Be(StaffUserId);
        _added.Name.Should().Be(DayTemplateName);
        _added.IsArchived.Should().BeFalse();
        _added.Blocks.Should().HaveCount(2);
        _added.Blocks.Should().OnlyContain(b => b.DayTemplateId == _added.Id && b.ValidFrom == Today && b.ValidTo == null);
        _added.Blocks.Should().ContainSingle(b => b.ActivityTypeId == Training.Id && b.Note == TemplateNote);
        _added.Blocks.Should().ContainSingle(b => b.ActivityTypeId == Lunch.Id && b.Note == string.Empty);

        response.Id.Should().Be(_added.Id);
        response.OwnerUserId.Should().Be(StaffUserId);
        response.Name.Should().Be(DayTemplateName);
        response.Blocks.Select(b => b.ActivityName).Should().Equal(Lunch.Name, Training.Name);
        response.Blocks[1].Note.Should().Be(TemplateNote);
        response.WeekTemplateCount.Should().Be(0);
        response.CanBeDeleted.Should().BeTrue();
        response.CanEdit.Should().BeTrue();

        Identity.Verify(i => i.GetStaffAsync(Token), Times.Once);
        Repository.Verify(r => r.DayTemplateNameExistsAsync(StaffUserId, DayTemplateName, null, Token), Times.Once);
        Repository.Verify(r => r.GetActivityTypesByIdAsync(It.IsAny<IEnumerable<Guid>>(), Token), Times.Once);
        Repository.Verify(r => r.AddDayTemplateAsync(_added, Token), Times.Once);
        Repository.Verify(r => r.GetDayTemplateAsync(_added.Id, Today, Token), Times.Once);
    }

    [Test]
    public async Task Handle_AdminForSomeoneElse_AddsItToTheirTemplates()
    {
        var response = await Handler.Handle(Command(ownerUserId: OtherStaffUserId, actor: AdminActor), Token);

        _added!.OwnerUserId.Should().Be(OtherStaffUserId);
        response.OwnerUserId.Should().Be(OtherStaffUserId);
        response.CanEdit.Should().BeTrue();
        Repository.Verify(r => r.DayTemplateNameExistsAsync(OtherStaffUserId, DayTemplateName, null, Token), Times.Once);
    }

    [Test]
    public async Task Handle_OtherStaffForSomeoneElse_ThrowsForbiddenAndAddsNothing()
    {
        var act = () => Handler.Handle(Command(actor: OtherStaffActor), Token);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage(CalendarActor.NotYourTemplates);
        _added.Should().BeNull();
        Identity.Verify(i => i.GetStaffAsync(It.IsAny<CancellationToken>()), Times.Never);
        Repository.Verify(r => r.AddDayTemplateAsync(It.IsAny<DayTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_OwnerIsNotStaff_ThrowsNotFoundAndAddsNothing()
    {
        var stranger = Guid.NewGuid();

        var act = () => Handler.Handle(Command(ownerUserId: stranger, actor: AdminActor), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"*{stranger}*");
        _added.Should().BeNull();
        Repository.Verify(
            r => r.DayTemplateNameExistsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Handle_NoBlocks_AddsAnEmptyTemplate()
    {
        var response = await Handler.Handle(Command(blocks: []), Token);

        _added!.Blocks.Should().BeEmpty();
        response.Blocks.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_NameTakenAmongOwnersTemplates_ThrowsConflictAndAddsNothing()
    {
        Repository
            .Setup(r => r.DayTemplateNameExistsAsync(StaffUserId, DayTemplateName, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => Handler.Handle(Command(), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*'{DayTemplateName}' already exists*");
        _added.Should().BeNull();
    }

    [Test]
    public async Task Handle_NameTakenOnlyBySomeoneElse_AddsIt()
    {
        Repository
            .Setup(r => r.DayTemplateNameExistsAsync(OtherStaffUserId, DayTemplateName, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Handler.Handle(Command(), Token);

        _added.Should().NotBeNull();
    }

    [Test]
    public async Task Handle_UnknownActivityType_ThrowsNotFoundAndAddsNothing()
    {
        var unknown = Guid.NewGuid();

        var act = () => Handler.Handle(Command(blocks: [BlockValue(12, 13, unknown)]), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"*{unknown}*");
        _added.Should().BeNull();
    }

    [Test]
    public async Task Handle_ArchivedActivityType_ThrowsConflictAndAddsNothing()
    {
        var archived = CreateArchivedActivityType();
        GivenActivityTypes(archived);

        var act = () => Handler.Handle(Command(blocks: [BlockValue(12, 13, archived.Id)]), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*{archived.Name}*");
        _added.Should().BeNull();
    }
}
