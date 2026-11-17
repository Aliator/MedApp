using AutoFixture;
using FluentAssertions;
using MedApp.Application.Notepads.Common;
using MedApp.Application.Notepads.Queries.GetNotepad;
using MedApp.Application.Notepads.Repositories;
using MedApp.Domain.Notepads;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.Extensions.Options;
using Moq;
using static MedApp.UnitTests.Common.Constants.NotepadTestConstants;

namespace MedApp.UnitTests.Application.Notepads.Queries.GetNotepad;

[TestFixture]
public sealed class GetNotepadHandlerTests
{
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

    private GetNotepadHandler Handler => _fixture.Create<GetNotepadHandler>();

    private void Given(Notepad? notepad) =>
        _repository.Setup(r => r.GetForUpdateAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(notepad);

    [Test]
    public async Task Handle_NoNotepad_ReturnsAnEmptyOneAtVersionZero()
    {
        Given(null);

        var response = await Handler.Handle(new GetNotepadQuery(UserId), CancellationToken.None);

        response.Should().BeEquivalentTo(new { Text = string.Empty, LastUpdatedUtc = (DateTime?)null, Version = 0, KeepDays });
    }

    [Test]
    public async Task Handle_Notepad_ReturnsIt()
    {
        var notepad = CreateNotepad(daysAgo: KeepDays - 1);
        Given(notepad);

        var response = await Handler.Handle(new GetNotepadQuery(UserId), CancellationToken.None);

        response.Should().BeEquivalentTo(new { Text, LastUpdatedUtc = (DateTime?)notepad.LastUpdatedUtc, Version = 3, KeepDays });
        _repository.Verify(r => r.DeleteAsync(It.IsAny<Notepad>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase(KeepDays)]
    [TestCase(KeepDays + 10)]
    public async Task Handle_NoChangeForKeepDays_ClearsItFirst(double daysAgo)
    {
        var notepad = CreateNotepad(daysAgo);
        Given(notepad);
        using var cts = new CancellationTokenSource();

        var response = await Handler.Handle(new GetNotepadQuery(UserId), cts.Token);

        response.Text.Should().BeEmpty();
        response.Version.Should().Be(0);
        _repository.Verify(r => r.DeleteAsync(notepad, cts.Token), Times.Once);
        _repository.Verify(r => r.GetForUpdateAsync(UserId, cts.Token), Times.Once);
    }
}
