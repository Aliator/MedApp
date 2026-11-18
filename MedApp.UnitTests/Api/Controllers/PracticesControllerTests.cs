using AutoFixture;
using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Practices.Commands.CreatePractice;
using MedApp.Application.Practices.Commands.DeletePractice;
using MedApp.Application.Practices.Commands.SetPracticeArchived;
using MedApp.Application.Practices.Commands.UpdatePractice;
using MedApp.Application.Practices.Queries.GetPracticeById;
using MedApp.Application.Practices.Queries.GetPractices;
using MedApp.Contracts.Practices.Requests;
using MedApp.Contracts.Practices.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;
using static MedApp.UnitTests.Common.Constants.PracticesTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class PracticesControllerTests
{
    /// <summary>Holders of EditPatients keep the directory; holders of ViewPatients read it.</summary>
    private static readonly string[] Changes =
    [
        nameof(PracticesController.CreatePractice),
        nameof(PracticesController.UpdatePractice),
        nameof(PracticesController.ArchivePractice),
        nameof(PracticesController.RestorePractice),
        nameof(PracticesController.DeletePractice),
    ];

    private IFixture _fixture = null!;
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _cts = new CancellationTokenSource();
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    private static PracticesController Controller(RecordingMediator mediator) =>
        new PracticesController(mediator).WithRequest(SignedIn("User"));

    private async Task ExpectOk(object response, Func<object?, bool> request, Func<PracticesController, Task<IActionResult>> act)
    {
        var mediator = new RecordingMediator(_ => response);

        var result = await act(Controller(mediator));

        request(mediator.Request).Should().BeTrue(mediator.Request?.ToString());
        mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task GetPractices_AsksForArchivedOnesOrNot(bool includeArchived)
    {
        IReadOnlyList<PracticeResponse> practices = [];
        await ExpectOk(practices, r => Equals(r, new GetPracticesQuery(includeArchived)), c => c.GetPractices(includeArchived, _cts.Token));
    }

    [Test]
    public async Task GetPractice_ReturnsIt() =>
        await ExpectOk(_fixture.Create<PracticeResponse>(), r => Equals(r, new GetPracticeByIdQuery(RecordId)),
            c => c.GetPractice(RecordId, _cts.Token));

    [Test]
    public async Task CreatePractice_SendsEveryDetail_AndReturnsItWithItsAddress()
    {
        var practice = _fixture.Create<PracticeResponse>();
        var mediator = new RecordingMediator(_ => practice);
        var request = new CreatePracticeRequest
        {
            Name = PracticeName, OdsCode = OdsCode, AddressLines = [AddressLine1], Postcode = Postcode, Phone = Phone,
            SecureEmail = SecureEmail
        };

        var result = await Controller(mediator).CreatePractice(request, _cts.Token);

        mediator.Request.Should().BeEquivalentTo(
            new CreatePracticeCommand(PracticeName, OdsCode, [AddressLine1], Postcode, Phone, SecureEmail));
        mediator.Tokens.Single().Should().Be(_cts.Token);
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/practices/{practice.Id}");
        created.Value.Should().Be(practice);
    }

    [Test]
    public async Task UpdatePractice_SendsTheDetailsAndTheVersionSeen() =>
        await ExpectOk(_fixture.Create<PracticeResponse>(),
            r => r is UpdatePracticeCommand { Name: OtherPracticeName, OdsCode: OtherOdsCode, ExpectedVersion: 4, Phone: Phone } q
                 && q.PracticeId == RecordId && q.AddressLines!.SequenceEqual([AddressLine2]) && q.SecureEmail == null,
            c => c.UpdatePractice(RecordId, new UpdatePracticeRequest
            {
                Name = OtherPracticeName, OdsCode = OtherOdsCode, AddressLines = [AddressLine2], Phone = Phone, ExpectedVersion = 4
            }, _cts.Token));

    [Test]
    public async Task ArchiveAndRestorePractice_SetItsArchivedFlagFromTheVersionSeen()
    {
        var practice = _fixture.Create<PracticeResponse>();
        var request = new PracticeVersionRequest { ExpectedVersion = 2 };
        await ExpectOk(practice, r => Equals(r, new SetPracticeArchivedCommand(RecordId, true, 2)),
            c => c.ArchivePractice(RecordId, request, _cts.Token));
        await ExpectOk(practice, r => Equals(r, new SetPracticeArchivedCommand(RecordId, false, 2)),
            c => c.RestorePractice(RecordId, request, _cts.Token));
    }

    [Test]
    public async Task DeletePractice_ReturnsNoContent()
    {
        var mediator = new RecordingMediator();

        var result = await Controller(mediator).DeletePractice(RecordId, _cts.Token);

        mediator.Request.Should().Be(new DeletePracticeCommand(RecordId));
        mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<NoContentResult>();
    }

    [Test]
    public async Task Changes_NeedEditPatients()
    {
        foreach (var action in Changes)
        {
            (await ControllerAuthorisation.MayCallAsync<PracticesController>(action, SignedIn("User"))).Should().BeTrue(action);
            (await ControllerAuthorisation.MayCallAsync<PracticesController>(
                action, SignedInWith(MedApp.Application.Common.Identity.Permission.ViewPatients))).Should().BeFalse(action);
            (await ControllerAuthorisation.MayCallAsync<PracticesController>(action, SignedIn())).Should().BeFalse(action);
        }
    }

    [Test]
    public async Task Reading_NeedsViewPatients()
    {
        var reads = ControllerAuthorisation.Actions<PracticesController>().Except(Changes).ToList();
        reads.Should().BeEquivalentTo([nameof(PracticesController.GetPractices), nameof(PracticesController.GetPractice)]);

        foreach (var action in reads)
        {
            (await ControllerAuthorisation.MayCallAsync<PracticesController>(
                action, SignedInWith(MedApp.Application.Common.Identity.Permission.ViewPatients))).Should().BeTrue(action);
            (await ControllerAuthorisation.MayCallAsync<PracticesController>(action, SignedIn())).Should().BeFalse(action);
        }
    }
}
