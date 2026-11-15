using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.DeletePatientProcedureStageTemplate;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedureStages.Templates.Commands.DeletePatientProcedureStageTemplate;

[TestFixture]
public sealed class DeletePatientProcedureTemplateHandlerTests
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
    public async Task Handle_TemplateUsedByProcedures_ThrowsConflictSuggestingArchiving()
    {
        var template = CreateTemplate();
        _repository.Setup(r => r.GetStageTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        _repository
            .Setup(r => r.GetTemplateProcedureCountsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [template.Id] = 2 });

        var act = () => _fixture.Create<DeletePatientProcedureTemplateHandler>()
            .Handle(new DeletePatientProcedureTemplateCommand(template.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*Archive it instead*");
    }

    [Test]
    public async Task Handle_TemplateUnused_DeletesIt()
    {
        var template = CreateTemplate();
        _repository.Setup(r => r.GetStageTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        using var cts = new CancellationTokenSource();

        await _fixture.Create<DeletePatientProcedureTemplateHandler>()
            .Handle(new DeletePatientProcedureTemplateCommand(template.Id), cts.Token);

        _repository.Verify(r => r.DeleteStageTemplateAsync(template.Id, cts.Token), Times.Once);
    }

    [Test]
    public async Task Handle_TemplateDoesNotExist_ThrowsNotFound()
    {
        var act = () => _fixture.Create<DeletePatientProcedureTemplateHandler>()
            .Handle(new DeletePatientProcedureTemplateCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
