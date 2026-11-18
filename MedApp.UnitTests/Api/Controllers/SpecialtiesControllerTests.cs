using AutoFixture;
using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Specialties.Commands.CreateSpecialty;
using MedApp.Application.Specialties.Commands.DeleteSpecialty;
using MedApp.Application.Specialties.Commands.SetSpecialtyArchived;
using MedApp.Application.Specialties.Commands.UpdateSpecialty;
using MedApp.Application.Specialties.Queries.GetSpecialties;
using MedApp.Application.Specialties.Queries.GetSpecialtyById;
using MedApp.Contracts.Specialties.Requests;
using MedApp.Contracts.Specialties.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;
using static MedApp.UnitTests.Common.Constants.SpecialtiesTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class SpecialtiesControllerTests
{
    /// <summary>Specialties are organisation-wide, so only admins change them; any staff read them.</summary>
    private static readonly string[] AdminOnly =
    [
        nameof(SpecialtiesController.CreateSpecialty),
        nameof(SpecialtiesController.UpdateSpecialty),
        nameof(SpecialtiesController.ArchiveSpecialty),
        nameof(SpecialtiesController.RestoreSpecialty),
        nameof(SpecialtiesController.DeleteSpecialty),
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

    private static SpecialtiesController Controller(RecordingMediator mediator) =>
        new SpecialtiesController(mediator).WithRequest(SignedIn("Admin"));

    private async Task ExpectOk(object response, object request, Func<SpecialtiesController, Task<IActionResult>> act)
    {
        var mediator = new RecordingMediator(_ => response);

        var result = await act(Controller(mediator));

        mediator.Request.Should().Be(request);
        mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task GetSpecialties_AsksForArchivedOnesOrNot(bool includeArchived)
    {
        IReadOnlyList<SpecialtyResponse> specialties = [];
        await ExpectOk(specialties, new GetSpecialtiesQuery(includeArchived), c => c.GetSpecialties(includeArchived, _cts.Token));
    }

    [Test]
    public async Task GetSpecialty_ReturnsIt() =>
        await ExpectOk(_fixture.Create<SpecialtyResponse>(), new GetSpecialtyByIdQuery(RecordId), c => c.GetSpecialty(RecordId, _cts.Token));

    [Test]
    public async Task CreateSpecialty_ReturnsItWithItsAddress()
    {
        var specialty = _fixture.Create<SpecialtyResponse>();
        var mediator = new RecordingMediator(_ => specialty);

        var result = await Controller(mediator).CreateSpecialty(new CreateSpecialtyRequest { Name = SpecialtyName }, _cts.Token);

        mediator.Request.Should().Be(new CreateSpecialtyCommand(SpecialtyName));
        mediator.Tokens.Single().Should().Be(_cts.Token);
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/specialties/{specialty.Id}");
        created.Value.Should().Be(specialty);
    }

    [Test]
    public async Task UpdateSpecialty_SendsTheNameAndTheVersionSeen() =>
        await ExpectOk(_fixture.Create<SpecialtyResponse>(),
            new UpdateSpecialtyCommand(RecordId, OtherSpecialtyName, 4),
            c => c.UpdateSpecialty(RecordId, new UpdateSpecialtyRequest { Name = OtherSpecialtyName, ExpectedVersion = 4 }, _cts.Token));

    [Test]
    public async Task ArchiveAndRestoreSpecialty_SetItsArchivedFlagFromTheVersionSeen()
    {
        var specialty = _fixture.Create<SpecialtyResponse>();
        var request = new SpecialtyVersionRequest { ExpectedVersion = 2 };
        await ExpectOk(specialty, new SetSpecialtyArchivedCommand(RecordId, true, 2), c => c.ArchiveSpecialty(RecordId, request, _cts.Token));
        await ExpectOk(specialty, new SetSpecialtyArchivedCommand(RecordId, false, 2), c => c.RestoreSpecialty(RecordId, request, _cts.Token));
    }

    [Test]
    public async Task DeleteSpecialty_ReturnsNoContent()
    {
        var mediator = new RecordingMediator();

        var result = await Controller(mediator).DeleteSpecialty(RecordId, _cts.Token);

        mediator.Request.Should().Be(new DeleteSpecialtyCommand(RecordId));
        mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<NoContentResult>();
    }

    [Test]
    public async Task Changes_AdminsOnly()
    {
        foreach (var action in AdminOnly)
        {
            (await ControllerAuthorisation.MayCallAsync<SpecialtiesController>(action, SignedIn("User"))).Should().BeFalse(action);
            (await ControllerAuthorisation.MayCallAsync<SpecialtiesController>(action, SignedIn("Admin"))).Should().BeTrue(action);
            (await ControllerAuthorisation.MayCallAsync<SpecialtiesController>(action, SignedIn())).Should().BeFalse(action);
        }
    }

    [Test]
    public async Task Reading_AnyStaff()
    {
        var anyStaff = ControllerAuthorisation.Actions<SpecialtiesController>().Except(AdminOnly).ToList();
        anyStaff.Should().BeEquivalentTo([nameof(SpecialtiesController.GetSpecialties), nameof(SpecialtiesController.GetSpecialty)]);

        foreach (var action in anyStaff)
        {
            (await ControllerAuthorisation.MayCallAsync<SpecialtiesController>(action, SignedIn("User"))).Should().BeTrue(action);
            (await ControllerAuthorisation.MayCallAsync<SpecialtiesController>(action, SignedIn())).Should().BeFalse(action);
        }
    }
}
