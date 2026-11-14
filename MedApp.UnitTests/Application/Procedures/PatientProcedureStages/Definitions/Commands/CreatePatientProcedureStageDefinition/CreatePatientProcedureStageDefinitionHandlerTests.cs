using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.CreatePatientProcedureStageDefinition;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedureStages.Definitions.Commands.CreatePatientProcedureStageDefinition;

[TestFixture]
public sealed class CreatePatientProcedureStageDefinitionHandlerTests
{
    private Mock<IPatientProcedureStagesRepository> _repository = null!;
    private IFixture _fixture = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _repository = new Mock<IPatientProcedureStagesRepository>();
        _repository
            .Setup(r => r.GetStageDefinitionUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, StageDefinitionUsage>());
        _repository
            .Setup(r => r.GetTemplateProcedureCountsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int>());
        _fixture.Inject(_repository.Object);
    }

    private void GivenUsage(Guid id, int templates, int procedures) =>
        _repository
            .Setup(r => r.GetStageDefinitionUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, StageDefinitionUsage> { [id] = new(templates, procedures) });

    [Test]
    public async Task Handle_NameAlreadyUsed_ThrowsConflict()
    {
        _repository.Setup(r => r.StageDefinitionNameExistsAsync(ConsultationStage, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => _fixture.Create<CreatePatientProcedureStageDefinitionHandler>()
            .Handle(new CreatePatientProcedureStageDefinitionCommand(ConsultationStage, "d", "i"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _repository.Verify(r => r.AddStageDefinitionAsync(It.IsAny<PatientProcedureStageDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_NewName_AddsTheDefinitionTrimmed()
    {
        using var cts = new CancellationTokenSource();

        var response = await _fixture.Create<CreatePatientProcedureStageDefinitionHandler>()
            .Handle(new CreatePatientProcedureStageDefinitionCommand($" {ConsultationStage} ", "d", "i"), cts.Token);

        response.Name.Should().Be(ConsultationStage);
        response.IsArchived.Should().BeFalse();
        response.CanBeDeleted.Should().BeTrue();
        _repository.Verify(r => r.AddStageDefinitionAsync(It.Is<PatientProcedureStageDefinition>(d => d.Name == ConsultationStage), cts.Token), Times.Once);
    }
}
