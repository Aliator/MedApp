using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.DayTemplates.Commands.UpdateDayTemplate;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarSetupTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.DayTemplates.Commands.UpdateDayTemplate;

[TestFixture]
public sealed class UpdateDayTemplateHandlerTests : CalendarSetupHandlerTestBase
{
    private DayTemplateBlock _lunch = null!;
    private DayTemplate _template = null!;
    private TemplateHistoryChange<DayTemplateBlock>? _savedChange;
    private int _saves;

    private UpdateDayTemplateHandler Handler => Fixture.Create<UpdateDayTemplateHandler>();

    [SetUp]
    public void SetUp()
    {
        _lunch = CreateTemplateBlock(12, 13, Lunch, note: TemplateNote);
        _template = BuildDayTemplate([_lunch]);
        _savedChange = null;
        _saves = 0;
        GivenActivityTypes(Lunch, Training);

        Repository.Setup(r => r.GetDayTemplateForUpdateAsync(DayTemplateId, Today, It.IsAny<CancellationToken>())).ReturnsAsync(_template);
        Repository.Setup(r => r.GetDayTemplateAsync(DayTemplateId, Today, It.IsAny<CancellationToken>())).ReturnsAsync(_template);
        Repository
            .Setup(r => r.SaveDayTemplateAsync(_template, It.IsAny<TemplateHistoryChange<DayTemplateBlock>?>(), It.IsAny<CancellationToken>()))
            .Callback((DayTemplate _, TemplateHistoryChange<DayTemplateBlock>? change, CancellationToken _) =>
            {
                _savedChange = change;
                _saves++;
            })
            .Returns(Task.CompletedTask);
    }

    /// <summary>A change to <see cref="StaffUserId"/>'s clinic day, by them.</summary>
    private static UpdateDayTemplateCommand Command(string name, params DayTemplateBlockValue[] blocks) =>
        new(DayTemplateId, name, blocks, StaffActor);

    [Test]
    public async Task Handle_SameBlocks_SavesWithNoChangeToTheBlocks()
    {
        // The same note, untidy, is the same block.
        await Handler.Handle(Command(DayTemplateName, BlockValue(12, 13, Lunch.Id, $" {TemplateNote}  ")), Token);

        _saves.Should().Be(1);
        _savedChange.Should().BeNull();
        Repository.Verify(r => r.SaveDayTemplateAsync(_template, null, Token), Times.Once);
    }

    [Test]
    public async Task Handle_BlockMoved_ClosesTheOldRowYesterdayAndAddsOneFromToday()
    {
        await Handler.Handle(Command(DayTemplateName, BlockValue(12, 14, Lunch.Id, TemplateNote)), Token);

        _savedChange.Should().NotBeNull();
        _savedChange!.Close.Should().Equal(_lunch);
        _savedChange.Delete.Should().BeEmpty();
        _savedChange.ClosedValidTo.Should().Be(Today.AddDays(-1));
        var added = _savedChange.Add.Should().ContainSingle().Subject;
        added.DayTemplateId.Should().Be(DayTemplateId);
        added.ActivityTypeId.Should().Be(Lunch.Id);
        added.StartTime.Should().Be(new TimeOnly(12, 0));
        added.EndTime.Should().Be(new TimeOnly(14, 0));
        added.Note.Should().Be(TemplateNote);
        added.ValidFrom.Should().Be(Today);
    }

    [Test]
    public async Task Handle_BlockAddedToday_IsReplacedNotClosed()
    {
        var addedToday = CreateTemplateBlock(15, 16, Training, validFrom: Today);
        _template.Blocks.Add(addedToday);

        await Handler.Handle(Command(DayTemplateName, BlockValue(12, 13, Lunch.Id, TemplateNote), BlockValue(15, 17, Training.Id)), Token);

        _savedChange!.Delete.Should().Equal(addedToday);
        _savedChange.Close.Should().BeEmpty();
        _savedChange.Add.Should().ContainSingle(b => b.EndTime == new TimeOnly(17, 0));
    }

    [Test]
    public async Task Handle_Renamed_ChecksTheNameAndSavesIt()
    {
        await Handler.Handle(Command($" {OtherDayTemplateName} ", BlockValue(12, 13, Lunch.Id, TemplateNote)), Token);

        _template.Name.Should().Be(OtherDayTemplateName);
        Repository.Verify(r => r.DayTemplateNameExistsAsync(StaffUserId, OtherDayTemplateName, DayTemplateId, Token), Times.Once);
    }

    [Test]
    public async Task Handle_AdminRenamesSomeoneElses_ChecksTheNameAmongTheOwnersTemplates()
    {
        await Handler.Handle(
            new UpdateDayTemplateCommand(DayTemplateId, OtherDayTemplateName, [BlockValue(12, 13, Lunch.Id, TemplateNote)], AdminActor),
            Token);

        _template.Name.Should().Be(OtherDayTemplateName);
        _saves.Should().Be(1);
        Repository.Verify(r => r.DayTemplateNameExistsAsync(StaffUserId, OtherDayTemplateName, DayTemplateId, Token), Times.Once);
    }

    [Test]
    public async Task Handle_OtherStaffChangesSomeoneElses_ThrowsForbiddenAndSavesNothing()
    {
        var act = () => Handler.Handle(
            new UpdateDayTemplateCommand(DayTemplateId, OtherDayTemplateName, [BlockValue(12, 14)], OtherStaffActor),
            Token);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage(CalendarActor.NotYourTemplates);
        _template.Name.Should().Be(DayTemplateName);
        _saves.Should().Be(0);
    }

    [Test]
    public async Task Handle_SameName_DoesNotCheckItIsTaken()
    {
        await Handler.Handle(Command(DayTemplateName, BlockValue(12, 13, Lunch.Id, TemplateNote)), Token);

        Repository.Verify(r => r.DayTemplateNameExistsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_NameTaken_ThrowsConflictAndSavesNothing()
    {
        Repository.Setup(r => r.DayTemplateNameExistsAsync(StaffUserId, OtherDayTemplateName, DayTemplateId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => Handler.Handle(Command(OtherDayTemplateName), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*'{OtherDayTemplateName}' already exists*");
        _saves.Should().Be(0);
    }

    [Test]
    public async Task Handle_ArchivedActivityTypeAlreadyInTheTemplate_KeepsIt()
    {
        var archived = CreateArchivedActivityType();
        GivenActivityTypes(archived);
        _template.Blocks.Add(CreateTemplateBlock(15, 16, archived));

        await Handler.Handle(Command(DayTemplateName, BlockValue(12, 13, Lunch.Id, TemplateNote), BlockValue(15, 17, archived.Id)), Token);

        _saves.Should().Be(1);
        _savedChange!.Add.Should().ContainSingle(b => b.ActivityTypeId == archived.Id);
    }

    [Test]
    public async Task Handle_ArchivedActivityTypeNewToTheTemplate_ThrowsConflictAndSavesNothing()
    {
        var archived = CreateArchivedActivityType();
        GivenActivityTypes(archived);

        var act = () => Handler.Handle(Command(DayTemplateName, BlockValue(15, 16, archived.Id)), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*{archived.Name}*");
        _saves.Should().Be(0);
    }

    [Test]
    public async Task Handle_UnknownActivityType_ThrowsNotFound()
    {
        var act = () => Handler.Handle(Command(DayTemplateName, BlockValue(15, 16, Guid.NewGuid())), Token);

        await act.Should().ThrowAsync<NotFoundException>();
        _saves.Should().Be(0);
    }

    [Test]
    public async Task Handle_TemplateDoesNotExist_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new UpdateDayTemplateCommand(Guid.NewGuid(), DayTemplateName, [], StaffActor), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("Day template*was not found.");
    }

    [Test]
    public async Task Handle_Saved_ReturnsTheTemplateAsSavedWithItsUsage()
    {
        GivenDayTemplateUsage(DayTemplateId, new DayTemplateUsage(3, true));

        var response = await Handler.Handle(Command(DayTemplateName, BlockValue(12, 13, Lunch.Id, TemplateNote)), Token);

        response.Id.Should().Be(DayTemplateId);
        response.WeekTemplateCount.Should().Be(3);
        response.CanBeDeleted.Should().BeFalse();
        response.OwnerUserId.Should().Be(StaffUserId);
        response.CanEdit.Should().BeTrue();
        Repository.Verify(r => r.GetDayTemplateAsync(DayTemplateId, Today, Token), Times.Once);
        Repository.Verify(r => r.GetDayTemplateUsageAsync(It.Is<IEnumerable<Guid>>(ids => ids.Single() == DayTemplateId), Today, Token), Times.Once);
        Repository.Verify(r => r.GetActivityTypesByIdAsync(It.IsAny<IEnumerable<Guid>>(), Token), Times.Once);
    }
}
