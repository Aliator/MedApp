using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using MedApp.Application.Pathways.PatientPathwayStages.Definitions.Commands.CreatePatientPathwayStageDefinition;

namespace MedApp.UnitTests.Application.Pathways.PatientPathwayStages.Definitions.Commands.CreatePatientPathwayStageDefinition;

[TestFixture]
public sealed class CreatePatientPathwayStageDefinitionHandlerTests
{
    private Mock<IPatientPathwayStagesRepository> _repository = null!;
    private IFixture _fixture = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _repository = new Mock<IPatientPathwayStagesRepository>();
        _repository
            .Setup(r => r.GetStageDefinitionUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, StageDefinitionUsage>());
        _repository
            .Setup(r => r.GetTemplatePathwayCountsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int>());
        _fixture.Inject(_repository.Object);
    }

    private void GivenUsage(Guid id, int templates, int pathways) =>
        _repository
            .Setup(r => r.GetStageDefinitionUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, StageDefinitionUsage> { [id] = new(templates, pathways) });

    [Test]
    public async Task Handle_NameAlreadyUsed_ThrowsConflict()
    {
        _repository.Setup(r => r.StageDefinitionNameExistsAsync(ConsultationStage, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => _fixture.Create<CreatePatientPathwayStageDefinitionHandler>()
            .Handle(new CreatePatientPathwayStageDefinitionCommand(ConsultationStage, "d", "i"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _repository.Verify(r => r.AddStageDefinitionAsync(It.IsAny<PatientPathwayStageDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_NewName_AddsTheDefinitionTrimmed()
    {
        using var cts = new CancellationTokenSource();

        var response = await _fixture.Create<CreatePatientPathwayStageDefinitionHandler>()
            .Handle(new CreatePatientPathwayStageDefinitionCommand($" {ConsultationStage} ", "d", "i"), cts.Token);

        response.Name.Should().Be(ConsultationStage);
        response.IsArchived.Should().BeFalse();
        response.CanBeDeleted.Should().BeTrue();
        _repository.Verify(r => r.AddStageDefinitionAsync(It.Is<PatientPathwayStageDefinition>(d => d.Name == ConsultationStage), cts.Token), Times.Once);
    }

    [TestCase("Required", VisitRequirement.Required)]
    [TestCase("suggested", VisitRequirement.Suggested)]
    [TestCase("None", VisitRequirement.None)]
    public async Task Handle_VisitRequirement_StoresAndReturnsIt(string given, VisitRequirement expected)
    {
        var response = await _fixture.Create<CreatePatientPathwayStageDefinitionHandler>()
            .Handle(new CreatePatientPathwayStageDefinitionCommand(ConsultationStage, "d", "i", VisitRequirement: given), CancellationToken.None);

        response.VisitRequirement.Should().Be(expected.ToString());
        _repository.Verify(r => r.AddStageDefinitionAsync(
            It.Is<PatientPathwayStageDefinition>(d => d.VisitRequirement == expected), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_NoVisitRequirementGiven_DefaultsToNone()
    {
        var response = await _fixture.Create<CreatePatientPathwayStageDefinitionHandler>()
            .Handle(new CreatePatientPathwayStageDefinitionCommand(ConsultationStage, "d", "i"), CancellationToken.None);

        response.VisitRequirement.Should().Be("None");
    }
}
