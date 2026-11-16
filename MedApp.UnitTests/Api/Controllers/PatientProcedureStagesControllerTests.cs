using AutoFixture;
using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.CreatePatientProcedureStageDefinition;
using MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.DeletePatientProcedureStageDefinition;
using MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.SetPatientProcedureStageDefinitionArchived;
using MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.UpdatePatientProcedureStageDefinition;
using MedApp.Application.Procedures.PatientProcedureStages.Definitions.Queries.GetAllPatientProcedureStageDefinitions;
using MedApp.Application.Procedures.PatientProcedureStages.Definitions.Queries.GetPatientProcedureStageDefinitionById;
using MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.CreatePatientProcedureStageTemplate;
using MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.DeletePatientProcedureStageTemplate;
using MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.SetPatientProcedureStageTemplateArchived;
using MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.UpdatePatientProcedureStageTemplate;
using MedApp.Application.Procedures.PatientProcedureStages.Templates.Queries.GetAllPatientProcedureStageTemplates;
using MedApp.Application.Procedures.PatientProcedureStages.Templates.Queries.GetPatientProcedureStageTemplateById;
using MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Requests;
using MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Responses;
using MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Requests;
using MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class PatientProcedureStagesControllerTests
{
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

    private static PatientProcedureStagesController Controller(RecordingMediator mediator) =>
        new PatientProcedureStagesController(mediator).WithRequest(SignedIn("Admin"));

    private async Task ExpectOk(object response, object request, Func<PatientProcedureStagesController, Task<IActionResult>> act)
    {
        var mediator = new RecordingMediator(_ => response);

        var result = await act(Controller(mediator));

        mediator.Request.Should().Be(request);
        mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
    }

    [Test]
    public async Task CreateDefinition_ReturnsItWithItsAddress()
    {
        var request = _fixture.Create<CreatePatientProcedureStageDefinitionRequest>();
        var definition = _fixture.Create<PatientProcedureStageDefinitionResponse>();
        var mediator = new RecordingMediator(_ => definition);

        var result = await Controller(mediator).CreatePatientProcedureStageDefinition(request, _cts.Token);

        mediator.Request.Should().Be(new CreatePatientProcedureStageDefinitionCommand(request.Name, request.Description, request.Instructions, request.VisitRequirement));
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/procedures/patient-procedure-stage/definitions/{definition.Id}");
        created.Value.Should().Be(definition);
    }

    [Test]
    public async Task UpdateDefinition_SendsTheChanges()
    {
        var request = _fixture.Create<UpdatePatientProcedureStageDefinitionRequest>();
        await ExpectOk(_fixture.Create<PatientProcedureStageDefinitionResponse>(),
            new UpdatePatientProcedureStageDefinitionCommand(RecordId, request.Name, request.Description, request.Instructions, request.VisitRequirement),
            c => c.UpdatePatientProcedureStageDefinition(RecordId, request, _cts.Token));
    }

    [Test]
    public async Task ArchiveAndRestoreDefinition_SetItsArchivedFlag()
    {
        var definition = _fixture.Create<PatientProcedureStageDefinitionResponse>();
        await ExpectOk(definition, new SetPatientProcedureStageDefinitionArchivedCommand(RecordId, true), c => c.ArchivePatientProcedureStageDefinition(RecordId, _cts.Token));
        await ExpectOk(definition, new SetPatientProcedureStageDefinitionArchivedCommand(RecordId, false), c => c.RestorePatientProcedureStageDefinition(RecordId, _cts.Token));
    }

    [Test]
    public async Task DeleteDefinition_ReturnsNoContent()
    {
        var mediator = new RecordingMediator();

        var result = await Controller(mediator).DeletePatientProcedureStageDefinition(RecordId, _cts.Token);

        mediator.Request.Should().Be(new DeletePatientProcedureStageDefinitionCommand(RecordId));
        result.Should().BeOfType<NoContentResult>();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task GetAllDefinitions_AsksForArchivedOnesOrNot(bool includeArchived)
    {
        IReadOnlyList<PatientProcedureStageDefinitionResponse> definitions = [];
        await ExpectOk(definitions, new GetAllPatientProcedureStageDefinitionsQuery(includeArchived), c => c.GetAllPatientProcedureStageDefinitions(includeArchived, _cts.Token));
    }

    [Test]
    public async Task GetDefinitionById_ReturnsIt() =>
        await ExpectOk(_fixture.Create<PatientProcedureStageDefinitionResponse>(), new GetPatientProcedureStageDefinitionByIdQuery(RecordId), c => c.GetPatientProcedureStageDefinitionById(RecordId, _cts.Token));

    [Test]
    public async Task CreateTemplate_ReturnsItWithItsAddress()
    {
        var request = _fixture.Create<CreatePatientProcedureStageTemplateRequest>();
        var template = _fixture.Create<PatientProcedureStageTemplateResponse>();
        var mediator = new RecordingMediator(_ => template);

        var result = await Controller(mediator).CreatePatientProcedureStageTemplate(request, _cts.Token);

        mediator.Request.Should().Be(new CreatePatientProcedureTemplateCommand(request.Name, request.StageDefinitionIdsInOrder));
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/procedures/patient-procedure-stage/templates/{template.Id}");
        created.Value.Should().Be(template);
    }

    [Test]
    public async Task UpdateTemplate_SendsTheChanges()
    {
        var request = _fixture.Create<UpdatePatientProcedureStageTemplateRequest>();
        await ExpectOk(_fixture.Create<PatientProcedureStageTemplateResponse>(),
            new UpdatePatientProcedureTemplateCommand(RecordId, request.Name, request.StageDefinitionIdsInOrder),
            c => c.UpdatePatientProcedureStageTemplate(RecordId, request, _cts.Token));
    }

    [Test]
    public async Task ArchiveAndRestoreTemplate_SetItsArchivedFlag()
    {
        var template = _fixture.Create<PatientProcedureStageTemplateResponse>();
        await ExpectOk(template, new SetPatientProcedureTemplateArchivedCommand(RecordId, true), c => c.ArchivePatientProcedureStageTemplate(RecordId, _cts.Token));
        await ExpectOk(template, new SetPatientProcedureTemplateArchivedCommand(RecordId, false), c => c.RestorePatientProcedureStageTemplate(RecordId, _cts.Token));
    }

    [Test]
    public async Task DeleteTemplate_ReturnsNoContent()
    {
        var mediator = new RecordingMediator();

        var result = await Controller(mediator).DeletePatientProcedureStageTemplate(RecordId, _cts.Token);

        mediator.Request.Should().Be(new DeletePatientProcedureTemplateCommand(RecordId));
        result.Should().BeOfType<NoContentResult>();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task GetAllTemplates_AsksForArchivedOnesOrNot(bool includeArchived)
    {
        IReadOnlyList<PatientProcedureStageTemplateResponse> templates = [];
        await ExpectOk(templates, new GetAllPatientProcedureTemplatesQuery(includeArchived), c => c.GetAllPatientProcedureStageTemplates(includeArchived, _cts.Token));
    }

    [Test]
    public async Task GetTemplateById_ReturnsIt() =>
        await ExpectOk(_fixture.Create<PatientProcedureStageTemplateResponse>(), new GetPatientProcedureTemplateByIdQuery(RecordId), c => c.GetPatientProcedureStageTemplateById(RecordId, _cts.Token));
}
