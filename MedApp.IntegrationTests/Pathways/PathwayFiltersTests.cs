using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Pathways.Insights.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Common;
using MedApp.Contracts.Pathways.Insights.Responses;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Requests;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MedApp.Contracts.Specialties.Requests;
using MedApp.Contracts.Specialties.Responses;
using MedApp.IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.IntegrationTests.Pathways;

/// <summary>
/// The pathway list's specialty and stage reached filters, and Insights' specialty, on SQL Server: the stage reached
/// is a correlated, ordered subquery, which only a real database runs (the unit tests run the same LINQ in memory and
/// check its SQL's shape).
/// </summary>
[TestFixture]
public sealed class PathwayFiltersTests : IntegrationTest
{
    private const string Search = "/api/pathways/patient-pathways/search";

    private HttpClient _admin = null!;

    [SetUp]
    public async Task SignInAsync() => _admin = await SignInAsAdminAsync();

    [Test]
    public async Task Search_StageReached_FindsPathwaysWhoseCurrentStageIsFromTheDefinition()
    {
        // Lab, then Clinic, then Lab again: a pathway at either Lab stage counts, but not one past them all.
        var lab = await CreateStageDefinitionAsync(_admin);
        var clinic = await CreateStageDefinitionAsync(_admin);
        var template = await CreateTemplateAsync(_admin, lab.Id, clinic.Id, lab.Id);
        var patient = await CreatePatientAsync(_admin);
        var atFirstLab = await CreatePathwayAsync(_admin, patient.Id, template.Id);
        var atClinic = await FinishedUpToAsync(await CreatePathwayAsync(_admin, patient.Id, template.Id), 1);
        var atSecondLab = await FinishedUpToAsync(await CreatePathwayAsync(_admin, patient.Id, template.Id), 2);
        var allFinished = await FinishedUpToAsync(await CreatePathwayAsync(_admin, patient.Id, template.Id), 3);
        var cancelledAtLab = await CreatePathwayAsync(_admin, patient.Id, template.Id);
        (await PostPathwayStatusAsync(_admin, cancelledAtLab.Id, "Cancelled", "Synthetic reason.")).StatusCode.Should().Be(HttpStatusCode.OK);

        var atLab = await SearchAsync($"StageDefinitionIds={lab.Id}&IncludeClosed=true");
        var atClinicOrLab = await SearchAsync($"StageDefinitionIds={lab.Id}&StageDefinitionIds={clinic.Id}");

        atLab.Items.Select(p => p.Id).Should().BeEquivalentTo([atFirstLab.Id, atSecondLab.Id]);
        atClinicOrLab.Items.Select(p => p.Id).Should().BeEquivalentTo([atFirstLab.Id, atClinic.Id, atSecondLab.Id]);
        atClinicOrLab.Items.Should().NotContain(p => p.Id == allFinished.Id || p.Id == cancelledAtLab.Id);
    }

    [Test]
    public async Task Search_Specialties_FindsPathwaysInAnyOfThem()
    {
        var skin = await CreateSpecialtyAsync();
        var feet = await CreateSpecialtyAsync();
        var patient = await CreatePatientAsync(_admin);
        var inSkin = await CreatePathwayAsync(_admin, patient.Id, (await CreateTemplateInAsync(skin.Id)).Id);
        var inFeet = await CreatePathwayAsync(_admin, patient.Id, (await CreateTemplateInAsync(feet.Id)).Id);
        await CreatePatientWithPathwayAsync(_admin);

        (await SearchAsync($"SpecialtyIds={skin.Id}")).Items.Select(p => p.Id).Should().Equal(inSkin.Id);
        (await SearchAsync($"SpecialtyIds={skin.Id}&SpecialtyIds={feet.Id}")).Items.Select(p => p.Id)
            .Should().BeEquivalentTo([inSkin.Id, inFeet.Id]);
    }

    [Test]
    public async Task Insights_Specialty_CountsOnlyItsPathwaysInSqlAndChecksIt()
    {
        var skin = await CreateSpecialtyAsync();
        var patient = await CreatePatientAsync(_admin);
        var template = await CreateTemplateInAsync(skin.Id);
        await CreatePathwayAsync(_admin, patient.Id, template.Id);
        await CreatePathwayAsync(_admin, patient.Id, template.Id);
        await CreatePatientWithPathwayAsync(_admin);

        var insights = await (await _admin.GetAsync($"/api/pathways/insights?Weeks=4&SpecialtyId={skin.Id}"))
            .ReadAsAsync<PathwayInsightsResponse>();
        var (from, to) = (DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddMinutes(1));
        var created = await RunInScopeAsync(services => services.GetRequiredService<IPathwayInsightsRepository>()
            .CountCreationsByHourAsync(new InsightsScope(SpecialtyId: skin.Id), from, to, CancellationToken.None));

        insights.SpecialtyId.Should().Be(skin.Id);
        insights.Open.Current.Should().Be(2);
        insights.Throughput[^1].Created.Should().Be(2);
        created.Sum(h => h.Count).Should().Be(2);
        (await _admin.GetAsync($"/api/pathways/insights?SpecialtyId={Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>Finishes the pathway's first <paramref name="stages"/> stages, in order.</summary>
    private async Task<PatientPathwayResponse> FinishedUpToAsync(PatientPathwayResponse pathway, int stages)
    {
        foreach (var stage in pathway.Stages.OrderBy(s => s.StageOrder).Take(stages))
            (await FinishStageAsync(_admin, pathway.Id, stage.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        return pathway;
    }

    private async Task<SpecialtyResponse> CreateSpecialtyAsync() =>
        await (await _admin.PostAsJsonAsync("/api/specialties", new CreateSpecialtyRequest { Name = UniqueName("Specialty") }))
            .ReadAsAsync<SpecialtyResponse>(HttpStatusCode.Created);

    private async Task<PathwayTemplateResponse> CreateTemplateInAsync(Guid specialtyId)
    {
        var stage = await CreateStageDefinitionAsync(_admin);
        return await (await _admin.PostAsJsonAsync("/api/pathways/patient-pathway-stage/templates", new CreatePathwayTemplateRequest
            {
                Name = UniqueName("Template"),
                SpecialtyId = specialtyId,
                StageDefinitionIdsInOrder = [stage.Id],
            }))
            .ReadAsAsync<PathwayTemplateResponse>(HttpStatusCode.Created);
    }

    private async Task<PagedResponse<PatientPathwaySummaryResponse>> SearchAsync(string query) =>
        await (await _admin.GetAsync($"{Search}?{query}")).ReadAsAsync<PagedResponse<PatientPathwaySummaryResponse>>();
}
