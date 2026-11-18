using FluentAssertions;
using MedApp.Application.Pathways.PatientPathwayStages.Definitions;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Pathways.PatientPathways;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Pathways.PatientPathwayStages.Definitions;

[TestFixture]
public sealed class StageDefinitionMapperTests
{
    private static readonly PatientPathwayStageDefinition Definition = new()
    {
        Id = ConsultationDefinitionId,
        Name = ConsultationStage,
        Description = "Synthetic description.",
        Instructions = "Synthetic **instructions**.",
        IsArchived = true,
    };

    [Test]
    public void ToResponse_UsedDefinition_ReturnsItsDetailsAndThatItCantBeDeleted()
    {
        var response = StageDefinitionMapper.ToResponse(Definition, new StageDefinitionUsage(TemplateCount: 2, PathwayCount: 0));

        response.Should().BeEquivalentTo(new
        {
            Definition.Id,
            Definition.Name,
            Definition.Description,
            Definition.Instructions,
            IsArchived = true,
            TemplateCount = 2,
            CanBeDeleted = false,
        });
    }

    [Test]
    public void ToResponse_UnusedDefinition_CanBeDeleted()
    {
        StageDefinitionMapper.ToResponse(Definition, StageDefinitionUsage.None).CanBeDeleted.Should().BeTrue();
    }

    [Test]
    public async Task ToResponseAsync_LooksUpItsUsage_AndTakesNoneWhenTheRepositoryHasNone()
    {
        var repository = new Mock<IPatientPathwayStagesRepository>();
        using var cts = new CancellationTokenSource();
        repository
            .Setup(r => r.GetStageDefinitionUsageAsync(It.Is<IEnumerable<Guid>>(ids => ids.Single() == Definition.Id), cts.Token))
            .ReturnsAsync(new Dictionary<Guid, StageDefinitionUsage>());

        var response = await StageDefinitionMapper.ToResponseAsync(Definition, repository.Object, cts.Token);

        response.TemplateCount.Should().Be(0);
        response.CanBeDeleted.Should().BeTrue();
    }

    [TestCase(VisitRequirement.None, "None")]
    [TestCase(VisitRequirement.Suggested, "Suggested")]
    [TestCase(VisitRequirement.Required, "Required")]
    public void ToResponse_Definition_GivesItsVisitRequirementByName(VisitRequirement requirement, string expected)
    {
        var definition = CreateDefinition(ConsultationStage).WithVisit(requirement);

        StageDefinitionMapper.ToResponse(definition, StageDefinitionUsage.None).VisitRequirement.Should().Be(expected);
    }
}
