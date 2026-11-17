using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Notepads.Commands.SaveNotepad;
using MedApp.Application.Notepads.Queries.GetNotepad;
using MedApp.Contracts.Notepads.Requests;
using MedApp.Contracts.Notepads.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class NotepadControllerTests
{
    private static readonly NotepadResponse Notepad = new() { Text = "Synthetic jotting.", Version = 2, KeepDays = 30 };

    [Test]
    public async Task GetNotepad_ReturnsTheSignedInUsersOwn()
    {
        var mediator = new RecordingMediator(_ => Notepad);
        using var cts = new CancellationTokenSource();

        var result = await new NotepadController(mediator).WithRequest(SignedIn("User")).GetNotepad(cts.Token);

        mediator.Request.Should().Be(new GetNotepadQuery(UserId));
        mediator.Tokens.Single().Should().Be(cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(Notepad);
    }

    [Test]
    public async Task SaveNotepad_SavesTheSignedInUsersOwn()
    {
        var mediator = new RecordingMediator(_ => Notepad);
        using var cts = new CancellationTokenSource();

        var result = await new NotepadController(mediator).WithRequest(SignedIn("User"))
            .SaveNotepad(new SaveNotepadRequest { Text = "Synthetic jotting.", ExpectedVersion = 1 }, cts.Token);

        mediator.Request.Should().Be(new SaveNotepadCommand(UserId, "Synthetic jotting.", 1));
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(Notepad);
    }

    [TestCase(nameof(NotepadController.GetNotepad))]
    [TestCase(nameof(NotepadController.SaveNotepad))]
    public async Task Actions_AreOpenToAnyoneSignedIn_WithNoRole(string action)
    {
        (await ControllerAuthorisation.MayCallAsync<NotepadController>(action, SignedIn())).Should().BeTrue();
    }
}
