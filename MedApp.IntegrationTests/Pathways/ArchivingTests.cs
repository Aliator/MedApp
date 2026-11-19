using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Contracts.Pathways.PatientPathwayStages.Definitions.Responses;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Requests;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MedApp.Domain.Teams;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.IntegrationTests.Pathways;

/// <summary>Stage definitions and templates in use are archived, not deleted; unused ones can be deleted.</summary>
[TestFixture]
public sealed class ArchivingTests : IntegrationTest
{
    private const string Definitions = "/api/pathways/patient-pathway-stage/definitions";
    private const string Templates = "/api/pathways/patient-pathway-stage/templates";

    private HttpClient _admin = null!;

    [SetUp]
    public async Task SignInAsync() => _admin = await SignInAsAdminAsync();

    [Test]
    public async Task DeleteDefinition_Unused_DeletesIt()
    {
        var definition = await CreateStageDefinitionAsync(_admin);
        definition.CanBeDeleted.Should().BeTrue();

        await (await _admin.DeleteAsync($"{Definitions}/{definition.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await (await _admin.GetAsync($"{Definitions}/{definition.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeleteDefinition_UsedByATemplate_Returns409AndKeepsIt()
    {
        var definition = await CreateStageDefinitionAsync(_admin);
        await CreateTemplateAsync(_admin, definition.Id);

        var response = await _admin.DeleteAsync($"{Definitions}/{definition.Id}");

        var problem = await response.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict);
        problem.Title.Should().Contain("Archive it instead");
        var kept = await (await _admin.GetAsync($"{Definitions}/{definition.Id}")).ReadAsAsync<PatientPathwayStageDefinitionResponse>();
        kept.Should().BeEquivalentTo(new { TemplateCount = 1, CanBeDeleted = false });
    }

    [Test]
    public async Task ArchiveDefinition_InUse_HidesItFromNewTemplatesUntilRestored()
    {
        var definition = await CreateStageDefinitionAsync(_admin);
        await CreateTemplateAsync(_admin, definition.Id);

        var archived = await (await _admin.PostAsync($"{Definitions}/{definition.Id}/archive", null))
            .ReadAsAsync<PatientPathwayStageDefinitionResponse>();

        archived.IsArchived.Should().BeTrue();
        (await ListDefinitionsAsync(includeArchived: false)).Should().NotContain(d => d.Id == definition.Id);
        (await ListDefinitionsAsync(includeArchived: true)).Should().Contain(d => d.Id == definition.Id);
        var refused = await _admin.PostAsJsonAsync(Templates, new CreatePathwayTemplateRequest
        {
            Name = UniqueName("Template"),
            TeamId = Team.GeneralId,
            StageDefinitionIdsInOrder = [definition.Id],
        });
        await refused.ShouldHaveStatusAsync(HttpStatusCode.Conflict);

        await (await _admin.PostAsync($"{Definitions}/{definition.Id}/restore", null)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await CreateTemplateAsync(_admin, definition.Id);
    }

    [Test]
    public async Task ListDefinitions_SeededDefault_IsArchivedAndOnlyListedWhenAsked()
    {
        (await ListDefinitionsAsync(includeArchived: false)).Should().NotContain(d => d.Id == TestEnvironment.SeededStageDefinitionId);
        (await ListDefinitionsAsync(includeArchived: true)).Should().ContainSingle(d => d.Id == TestEnvironment.SeededStageDefinitionId)
            .Which.IsArchived.Should().BeTrue();
    }

    [Test]
    public async Task DeleteTemplate_Unused_DeletesIt()
    {
        var template = await CreateTemplateWithStagesAsync(_admin, 1);

        await (await _admin.DeleteAsync($"{Templates}/{template.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await (await _admin.GetAsync($"{Templates}/{template.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeleteTemplate_UsedByAPathway_Returns409AndKeepsIt()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin, stages: 1);

        var response = await _admin.DeleteAsync($"{Templates}/{pathway.TemplateId}");

        await response.ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await (await _admin.GetAsync($"{Templates}/{pathway.TemplateId}")).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task ArchiveTemplate_InUse_StopsNewPathwaysButKeepsExistingOnes()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin, stages: 1);
        var templateId = pathway.TemplateId!.Value;

        var archived = await (await _admin.PostAsync($"{Templates}/{templateId}/archive", null))
            .ReadAsAsync<PathwayTemplateResponse>();

        archived.IsArchived.Should().BeTrue();
        var refused = await (await ClinicalAsync(_admin)).PostAsJsonAsync("/api/pathways/patient-pathways",
            new Contracts.Pathways.PatientPathways.Requests.CreatePatientPathwayRequest
            {
                PatientId = pathway.PatientId,
                TemplateId = templateId,
                DueDate = UkToday,
                Priority = "Low",
            });
        (await refused.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict)).Title.Should().Contain("archived");
        await (await _admin.GetAsync(PathwayUrl(pathway.Id))).ShouldHaveStatusAsync(HttpStatusCode.OK);
        (await (await _admin.GetAsync($"{Templates}")).ReadAsAsync<List<PathwayTemplateResponse>>())
            .Should().NotContain(t => t.Id == templateId);

        await (await _admin.PostAsync($"{Templates}/{templateId}/restore", null)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await CreatePathwayAsync(_admin, pathway.PatientId, templateId);
    }

    [Test]
    public async Task CreateDefinition_NameTaken_Returns409()
    {
        var definition = await CreateStageDefinitionAsync(_admin);

        var response = await _admin.PostAsJsonAsync(Definitions,
            new Contracts.Pathways.PatientPathwayStages.Definitions.Requests.CreatePatientPathwayStageDefinitionRequest
            {
                Name = definition.Name,
                Description = "Duplicate",
                Instructions = "Duplicate",
            });

        await response.ShouldHaveStatusAsync(HttpStatusCode.Conflict);
    }

    private async Task<List<PatientPathwayStageDefinitionResponse>> ListDefinitionsAsync(bool includeArchived) =>
        await (await _admin.GetAsync($"{Definitions}?includeArchived={includeArchived}"))
            .ReadAsAsync<List<PatientPathwayStageDefinitionResponse>>();
}
