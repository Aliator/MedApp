using MedApp.Application.Pathways.PatientPathwayStages.Templates;
using AutoFixture;
using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Pathways.PatientPathwayStages.Definitions.Commands.CreatePatientPathwayStageDefinition;
using MedApp.Application.Pathways.PatientPathwayStages.Definitions.Commands.DeletePatientPathwayStageDefinition;
using MedApp.Application.Pathways.PatientPathwayStages.Definitions.Commands.SetPatientPathwayStageDefinitionArchived;
using MedApp.Application.Pathways.PatientPathwayStages.Definitions.Commands.UpdatePatientPathwayStageDefinition;
using MedApp.Application.Pathways.PatientPathwayStages.Definitions.Queries.GetAllPatientPathwayStageDefinitions;
using MedApp.Application.Pathways.PatientPathwayStages.Definitions.Queries.GetPatientPathwayStageDefinitionById;
using MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.CreatePathwayTemplate;
using MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.DeletePathwayTemplate;
using MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.SetPathwayTemplateArchived;
using MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.UpdatePathwayTemplate;
using MedApp.Application.Pathways.PatientPathwayStages.Templates.Queries.GetAllPathwayTemplates;
using MedApp.Application.Pathways.PatientPathwayStages.Templates.Queries.GetPathwayTemplateById;
using MedApp.Contracts.Pathways.PatientPathwayStages.Definitions.Requests;
using MedApp.Contracts.Pathways.PatientPathwayStages.Definitions.Responses;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Requests;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class PatientPathwayStagesControllerTests
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

    private static PatientPathwayStagesController Controller(RecordingMediator mediator) =>
        new PatientPathwayStagesController(mediator).WithRequest(SignedIn("Admin"));

    private async Task ExpectOk(object response, object request, Func<PatientPathwayStagesController, Task<IActionResult>> act)
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
        var request = _fixture.Create<CreatePatientPathwayStageDefinitionRequest>();
        var definition = _fixture.Create<PatientPathwayStageDefinitionResponse>();
        var mediator = new RecordingMediator(_ => definition);

        var result = await Controller(mediator).CreatePatientPathwayStageDefinition(request, _cts.Token);

        mediator.Request.Should().Be(new CreatePatientPathwayStageDefinitionCommand(
            request.Name, request.Description, request.Instructions, request.SnomedCode, request.SnomedTerm, request.VisitRequirement,
            request.NeedsLetterKind, request.WaitsForResults));
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/pathways/patient-pathway-stage/definitions/{definition.Id}");
        created.Value.Should().Be(definition);
    }

    [Test]
    public async Task UpdateDefinition_SendsTheChanges()
    {
        var request = _fixture.Create<UpdatePatientPathwayStageDefinitionRequest>();
        await ExpectOk(_fixture.Create<PatientPathwayStageDefinitionResponse>(),
            new UpdatePatientPathwayStageDefinitionCommand(
                RecordId, request.Name, request.Description, request.Instructions, request.SnomedCode, request.SnomedTerm,
                request.VisitRequirement, request.NeedsLetterKind, request.WaitsForResults),
            c => c.UpdatePatientPathwayStageDefinition(RecordId, request, _cts.Token));
    }

    [Test]
    public async Task ArchiveAndRestoreDefinition_SetItsArchivedFlag()
    {
        var definition = _fixture.Create<PatientPathwayStageDefinitionResponse>();
        await ExpectOk(definition, new SetPatientPathwayStageDefinitionArchivedCommand(RecordId, true), c => c.ArchivePatientPathwayStageDefinition(RecordId, _cts.Token));
        await ExpectOk(definition, new SetPatientPathwayStageDefinitionArchivedCommand(RecordId, false), c => c.RestorePatientPathwayStageDefinition(RecordId, _cts.Token));
    }

    [Test]
    public async Task DeleteDefinition_ReturnsNoContent()
    {
        var mediator = new RecordingMediator();

        var result = await Controller(mediator).DeletePatientPathwayStageDefinition(RecordId, _cts.Token);

        mediator.Request.Should().Be(new DeletePatientPathwayStageDefinitionCommand(RecordId));
        result.Should().BeOfType<NoContentResult>();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task GetAllDefinitions_AsksForArchivedOnesOrNot(bool includeArchived)
    {
        IReadOnlyList<PatientPathwayStageDefinitionResponse> definitions = [];
        await ExpectOk(definitions, new GetAllPatientPathwayStageDefinitionsQuery(includeArchived), c => c.GetAllPatientPathwayStageDefinitions(includeArchived, _cts.Token));
    }

    [Test]
    public async Task GetDefinitionById_ReturnsIt() =>
        await ExpectOk(_fixture.Create<PatientPathwayStageDefinitionResponse>(), new GetPatientPathwayStageDefinitionByIdQuery(RecordId), c => c.GetPatientPathwayStageDefinitionById(RecordId, _cts.Token));

    [Test]
    public async Task CreateTemplate_ReturnsItWithItsAddress()
    {
        var request = _fixture.Create<CreatePathwayTemplateRequest>();
        request.StageOptions = null;
        var template = _fixture.Create<PathwayTemplateResponse>();
        var mediator = new RecordingMediator(_ => template);

        var result = await Controller(mediator).CreatePathwayTemplate(request, _cts.Token);

        mediator.Request.Should().Be(new CreatePathwayTemplateCommand(
            request.Name, request.SpecialtyId, request.StageDefinitionIdsInOrder, request.ClockStopStageOrder,
            RetentionClass: request.RetentionClass));
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/pathways/patient-pathway-stage/templates/{template.Id}");
        created.Value.Should().Be(template);
    }

    [Test]
    public async Task UpdateTemplate_SendsTheChanges()
    {
        var request = _fixture.Create<UpdatePathwayTemplateRequest>();
        request.StageOptions = null;
        await ExpectOk(_fixture.Create<PathwayTemplateResponse>(),
            new UpdatePathwayTemplateCommand(RecordId, request.Name, request.SpecialtyId, request.StageDefinitionIdsInOrder, request.ClockStopStageOrder,
                RetentionClass: request.RetentionClass),
            c => c.UpdatePathwayTemplate(RecordId, request, _cts.Token));
    }

    [Test]
    public async Task CreateTemplate_WithStageOptions_SendsEachAsTheCommandsOptions()
    {
        var request = _fixture.Create<CreatePathwayTemplateRequest>();
        request.StageOptions = [new() { ChoosesBranch = true }, new() { IsOptional = true, Branch = "Benign" }];
        var mediator = new RecordingMediator(_ => _fixture.Create<PathwayTemplateResponse>());

        await Controller(mediator).CreatePathwayTemplate(request, _cts.Token);

        mediator.Request.Should().BeOfType<CreatePathwayTemplateCommand>().Which.StageOptions.Should().Equal(
            new TemplateStageOptions(false, true, null), new TemplateStageOptions(true, false, "Benign"));
    }

    [Test]
    public async Task ArchiveAndRestoreTemplate_SetItsArchivedFlag()
    {
        var template = _fixture.Create<PathwayTemplateResponse>();
        await ExpectOk(template, new SetPathwayTemplateArchivedCommand(RecordId, true), c => c.ArchivePathwayTemplate(RecordId, _cts.Token));
        await ExpectOk(template, new SetPathwayTemplateArchivedCommand(RecordId, false), c => c.RestorePathwayTemplate(RecordId, _cts.Token));
    }

    [Test]
    public async Task DeleteTemplate_ReturnsNoContent()
    {
        var mediator = new RecordingMediator();

        var result = await Controller(mediator).DeletePathwayTemplate(RecordId, _cts.Token);

        mediator.Request.Should().Be(new DeletePathwayTemplateCommand(RecordId));
        result.Should().BeOfType<NoContentResult>();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task GetAllTemplates_AsksForArchivedOnesOrNot(bool includeArchived)
    {
        IReadOnlyList<PathwayTemplateResponse> templates = [];
        await ExpectOk(templates, new GetAllPathwayTemplatesQuery(includeArchived), c => c.GetAllPathwayTemplates(includeArchived, _cts.Token));
    }

    [Test]
    public async Task GetTemplateById_ReturnsIt() =>
        await ExpectOk(_fixture.Create<PathwayTemplateResponse>(), new GetPathwayTemplateByIdQuery(RecordId), c => c.GetPathwayTemplateById(RecordId, _cts.Token));
}
