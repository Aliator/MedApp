using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Notepads.Commands.SaveNotepad;
using MedApp.Application.Notepads.Common;
using MedApp.Application.Notepads.Repositories;
using MedApp.Domain.Notepads;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.Extensions.Options;
using Moq;
using static MedApp.UnitTests.Common.Constants.NotepadTestConstants;

namespace MedApp.UnitTests.Application.Notepads.Commands.SaveNotepad;

[TestFixture]
public sealed class SaveNotepadHandlerTests
{
    private const string Edited = "Synthetic jotting: order more dressings.";

    private IFixture _fixture = null!;
    private Mock<INotepadRepository> _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _repository = new Mock<INotepadRepository>();
        _fixture.Inject(_repository.Object);
        _fixture.Inject(Options.Create(new NotepadOptions { KeepDays = KeepDays }));
        _fixture.Inject<TimeProvider>(new FixedTimeProvider(Now));
    }

    private SaveNotepadHandler Handler => _fixture.Create<SaveNotepadHandler>();

    private void Given(Notepad? notepad) =>
        _repository.Setup(r => r.GetForUpdateAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(notepad);

    [Test]
    public async Task Handle_FirstSave_AddsVersionOne()
    {
        Given(null);
        Notepad? added = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<Notepad>(), It.IsAny<CancellationToken>()))
            .Callback((Notepad n, CancellationToken _) => added = n)
            .Returns(Task.CompletedTask);

        var response = await Handler.Handle(new SaveNotepadCommand(UserId, Text, 0), CancellationToken.None);

        added.Should().BeEquivalentTo(new { UserId, Text, LastUpdatedUtc = Now, Version = 1 });
        response.Should().BeEquivalentTo(new { Text, LastUpdatedUtc = (DateTime?)Now, Version = 1, KeepDays });
    }

    [Test]
    public async Task Handle_FromTheVersionSaved_SavesTheNextVersion()
    {
        var notepad = CreateNotepad(version: 3);
        Given(notepad);
        using var cts = new CancellationTokenSource();

        var response = await Handler.Handle(new SaveNotepadCommand(UserId, Edited, 3), cts.Token);

        notepad.Should().BeEquivalentTo(new { Text = Edited, LastUpdatedUtc = Now, Version = 4 });
        response.Version.Should().Be(4);
        _repository.Verify(r => r.SaveAsync(cts.Token), Times.Once);
        _repository.Verify(r => r.GetForUpdateAsync(UserId, cts.Token), Times.Once);
    }

    [Test]
    public async Task Handle_EmptyText_ClearsItButKeepsItsVersion()
    {
        var notepad = CreateNotepad(version: 3);
        Given(notepad);

        var response = await Handler.Handle(new SaveNotepadCommand(UserId, string.Empty, 3), CancellationToken.None);

        response.Text.Should().BeEmpty();
        response.Version.Should().Be(4);
    }

    [TestCase(2)]
    [TestCase(0)]
    public async Task Handle_SavedElsewhereSince_ThrowsConflictWithoutSaving(int expectedVersion)
    {
        var notepad = CreateNotepad(version: 3);
        Given(notepad);

        var act = () => Handler.Handle(new SaveNotepadCommand(UserId, Edited, expectedVersion), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(NotepadRules.ChangedMeanwhileMessage);
        notepad.Text.Should().Be(Text);
        _repository.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_NoneButTheTabHadOne_ThrowsConflict()
    {
        // Deleted since (cleared after the days without a change, by another tab's read).
        Given(null);

        var act = () => Handler.Handle(new SaveNotepadCommand(UserId, Edited, 5), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(NotepadRules.ChangedMeanwhileMessage);
        _repository.Verify(r => r.AddAsync(It.IsAny<Notepad>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_NoChangeForKeepDays_ClearsItAndSaysSo()
    {
        var notepad = CreateNotepad(daysAgo: KeepDays + 1, version: 3);
        Given(notepad);
        using var cts = new CancellationTokenSource();

        var act = () => Handler.Handle(new SaveNotepadCommand(UserId, Text + " More.", 3), cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(NotepadRules.ClearedMessage(KeepDays));
        _repository.Verify(r => r.DeleteAsync(notepad, cts.Token), Times.Once);
        _repository.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
