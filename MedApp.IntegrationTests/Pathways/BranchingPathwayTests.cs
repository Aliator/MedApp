using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Contracts.Common;
using MedApp.Contracts.Pathways.Insights.Responses;
using MedApp.Contracts.Pathways.PatientPathways.Requests;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Requests;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MedApp.Contracts.Pathways.Timeline.Responses;
using MedApp.Contracts.Patients.Responses;
using MedApp.Contracts.Specialties.Responses;
using MedApp.Domain.Specialties;
using MedApp.IntegrationTests.Common;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Pathways;

/// <summary>
/// Pathways that branch, through the API on SQL Server: templates with optional stages and branch points, choosing a
/// branch when finishing one, skipping an optional stage, reinstating a skipped one, and the queries that read skipped
/// stages (the stage filter, Insights' stage times, the timeline).
/// </summary>
[TestFixture]
public sealed class BranchingPathwayTests : IntegrationTest
{
    private const string TemplatesUrl = "/api/pathways/patient-pathway-stage/templates";

    private HttpClient _admin = null!;
    private StaffAccount _staff = null!;

    [SetUp]
    public async Task SignInAsync()
    {
        _admin = await SignInAsAdminAsync();
        _staff = await CreateStaffAsync(UserRole);
    }

    /// <summary>
    /// Results (a branch point), Benign letter (Benign), Treatment and Follow-up (Treatment; Follow-up optional), then
    /// Discharge, which always runs.
    /// </summary>
    private async Task<PathwayTemplateResponse> CreateBranchingTemplateAsync()
    {
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
            ids.Add((await CreateStageDefinitionAsync(_admin)).Id);

        var response = await _admin.PostAsJsonAsync(TemplatesUrl, new CreatePathwayTemplateRequest
        {
            Name = UniqueName("Branching"),
            SpecialtyId = Specialty.GeneralId,
            StageDefinitionIdsInOrder = ids,
            StageOptions =
            [
                new() { ChoosesBranch = true },
                new() { Branch = "Benign" },
                new() { Branch = "Treatment" },
                new() { Branch = "Treatment", IsOptional = true },
                new(),
            ],
        });
        return await response.ReadAsAsync<PathwayTemplateResponse>(HttpStatusCode.Created);
    }

    private async Task<PatientPathwayResponse> CreateBranchingPathwayAsync()
    {
        var template = await CreateBranchingTemplateAsync();
        var patient = await CreatePatientAsync(_staff.Client);
        return await CreatePathwayAsync(_staff.Client, patient.Id, template.Id);
    }

    [Test]
    public async Task Template_WithOptions_KeepsThemWhenAnEditLeavesThemOutAndClearsThemWhenSentPlain()
    {
        var template = await CreateBranchingTemplateAsync();

        var read = await (await _admin.GetAsync($"{TemplatesUrl}/{template.Id}")).ReadAsAsync<PathwayTemplateResponse>();
        read.Stages.Select(s => (s.IsOptional, s.ChoosesBranch, s.Branch)).Should().Equal(
            (false, true, null), (false, false, "Benign"), (false, false, "Treatment"), (true, false, "Treatment"), (false, false, null));

        // A page from before stage options sends the same stage list without them: they stay.
        var kept = await (await _admin.PatchAsJsonAsync($"{TemplatesUrl}/{template.Id}",
                new UpdatePathwayTemplateRequest { Name = UniqueName("Renamed"), StageDefinitionIdsInOrder = read.StageDefinitionIdsInOrder }))
            .ReadAsAsync<PathwayTemplateResponse>();
        kept.Stages.Select(s => (s.IsOptional, s.ChoosesBranch, s.Branch)).Should().Equal(
            read.Stages.Select(s => (s.IsOptional, s.ChoosesBranch, s.Branch)));

        var plain = await (await _admin.PatchAsJsonAsync($"{TemplatesUrl}/{template.Id}",
                new UpdatePathwayTemplateRequest
                {
                    StageDefinitionIdsInOrder = read.StageDefinitionIdsInOrder,
                    StageOptions = [new(), new(), new(), new(), new()],
                }))
            .ReadAsAsync<PathwayTemplateResponse>();
        plain.Stages.Should().AllSatisfy(s => (s.IsOptional, s.ChoosesBranch, s.Branch).Should().Be((false, false, (string?)null)));
    }

    [Test]
    public async Task Template_BranchPointWithOneBranch_Returns400()
    {
        var first = await CreateStageDefinitionAsync(_admin);
        var second = await CreateStageDefinitionAsync(_admin);
        var third = await CreateStageDefinitionAsync(_admin);

        var response = await _admin.PostAsJsonAsync(TemplatesUrl, new CreatePathwayTemplateRequest
        {
            Name = UniqueName("Branching"),
            SpecialtyId = Specialty.GeneralId,
            StageDefinitionIdsInOrder = [first.Id, second.Id, third.Id],
            StageOptions = [new() { ChoosesBranch = true }, new() { Branch = "Benign" }, new()],
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task FinishBranchPoint_ChoosingABranch_SkipsTheOthersAndTheFilterAndTimelineFollow()
    {
        var pathway = await CreateBranchingPathwayAsync();
        var branchPoint = pathway.Stages[0];
        branchPoint.NeedsBranchChoice.Should().BeTrue();
        branchPoint.Branches.Select(b => (b.Name, string.Join("|", b.StageNames))).Should().Equal(
            ("Benign", pathway.Stages[1].Name), ("Treatment", $"{pathway.Stages[2].Name}|{pathway.Stages[3].Name}"));

        // No branch is assumed: finishing without one is refused.
        await (await FinishStageAsync(_staff.Client, pathway.Id, branchPoint.Id)).ShouldHaveStatusAsync(HttpStatusCode.BadRequest);

        var finished = await (await FinishStageAsync(_staff.Client, pathway.Id, branchPoint.Id,
                new FinishPathwayStageRequest { Branch = "Treatment" }))
            .ReadAsAsync<PatientPathwayResponse>();

        finished.Stages.Select(s => s.State).Should().Equal("Completed", "Skipped", "NotStarted", "NotStarted", "NotStarted");
        finished.Stages[1].SkipReason.Should().Be("BranchNotChosen");
        finished.CurrentStageId.Should().Be(pathway.Stages[2].Id);

        // The stage reached, in SQL, passes over the skipped stage.
        (await SearchAsync($"StageDefinitionIds={pathway.Stages[2].StageDefinitionId}")).Items.Should().ContainSingle(p => p.Id == pathway.Id);
        (await SearchAsync($"StageDefinitionIds={pathway.Stages[1].StageDefinitionId}")).Items.Should().NotContain(p => p.Id == pathway.Id);

        var timeline = await (await _staff.Client.GetAsync($"/api/patients/{pathway.PatientId}/timeline?kinds=Stages"))
            .ReadAsAsync<PatientTimelinePageResponse>();
        var chosen = timeline.Items.Single(e => e.Action == "BranchChosen");
        chosen.Branch.Should().Be("Treatment");
        chosen.SkippedStageNames.Should().Equal(pathway.Stages[1].Name);
    }

    [Test]
    public async Task SkipAndReinstate_StaffSkipAnOptionalStageAndOnlyHoldersOfUnlockStagesReinstateIt()
    {
        var pathway = await CreateBranchingPathwayAsync();
        await (await FinishStageAsync(_staff.Client, pathway.Id, pathway.Stages[0].Id, new FinishPathwayStageRequest { Branch = "Treatment" }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await FinishStageAsync(_staff.Client, pathway.Id, pathway.Stages[2].Id)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        var followUp = pathway.Stages[3];

        // Other needs a detail.
        await (await SkipAsync(_staff.Client, pathway.Id, followUp.Id, "Other", null)).ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
        var skipped = await (await SkipAsync(_staff.Client, pathway.Id, followUp.Id, "PatientDeclined", "Synthetic: declined by phone."))
            .ReadAsAsync<PatientPathwayResponse>();
        skipped.Stages[3].State.Should().Be("Skipped");
        skipped.Stages[3].SkipDetail.Should().Be("Synthetic: declined by phone.");
        skipped.CurrentStageId.Should().Be(pathway.Stages[4].Id);

        // A stage that isn't optional can't be skipped.
        await (await SkipAsync(_staff.Client, pathway.Id, pathway.Stages[4].Id, "NotNeeded", null)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);

        // Reinstating needs UnlockStages, which the built-in Admin role keeps.
        await (await ReinstateAsync(_staff.Client, pathway.Id, followUp.Id)).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        var reinstated = await (await ReinstateAsync(_admin, pathway.Id, followUp.Id)).ReadAsAsync<PatientPathwayResponse>();
        reinstated.CurrentStageId.Should().Be(followUp.Id);
        reinstated.Stages[3].State.Should().Be("NotStarted");

        // Staff see why it was skipped by name, but not the detail or why it was reinstated; admins see both.
        var staffView = await (await _staff.Client.GetAsync($"/api/patients/{pathway.PatientId}/timeline?kinds=Stages"))
            .ReadAsAsync<PatientTimelinePageResponse>();
        var adminView = await (await _admin.GetAsync($"/api/patients/{pathway.PatientId}/timeline?kinds=Stages"))
            .ReadAsAsync<PatientTimelinePageResponse>();
        staffView.Items.Single(e => e.Action == "StageSkipped").Should().Match<PatientTimelineEntryResponse>(e => e.SkipReason == "PatientDeclined" && e.Reason == null);
        staffView.Items.Single(e => e.Action == "StageReinstated").Reason.Should().BeNull();
        adminView.Items.Single(e => e.Action == "StageSkipped").Reason.Should().Be("Synthetic: declined by phone.");
        adminView.Items.Single(e => e.Action == "StageReinstated").Reason.Should().Be("Synthetic: needed after all.");
    }

    [Test]
    public async Task Insights_StageAfterASkippedOne_IsTimedFromTheSkipAndSkippedStagesAreLeftOut()
    {
        var pathway = await CreateBranchingPathwayAsync();
        await (await FinishStageAsync(_staff.Client, pathway.Id, pathway.Stages[0].Id, new FinishPathwayStageRequest { Branch = "Benign" }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await FinishStageAsync(_staff.Client, pathway.Id, pathway.Stages[1].Id)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await FinishStageAsync(_staff.Client, pathway.Id, pathway.Stages[4].Id)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        var insights = await (await _admin.GetAsync($"/api/pathways/insights?Weeks=4&TemplateId={pathway.TemplateId}"))
            .ReadAsAsync<PathwayInsightsResponse>();

        insights.StageTimes.Single().Stages.Select(s => s.StageOrder).Should().Equal(1, 2, 5);
        insights.StageTimes.Single().Stages.Should().AllSatisfy(s => s.AverageWaitingHours.Should().BeGreaterThanOrEqualTo(0));
    }

    [Test]
    public async Task Export_SkippedStage_ListsWhenWhoAndWhy()
    {
        var pathway = await CreateBranchingPathwayAsync();
        await (await FinishStageAsync(_staff.Client, pathway.Id, pathway.Stages[0].Id, new FinishPathwayStageRequest { Branch = "Benign" }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        var export = await (await _admin.GetAsync($"/api/patients/{pathway.PatientId}/export")).ReadAsAsync<PatientExportResponse>();

        var stages = export.Pathways.Single().Stages;
        stages.Where(s => s.SkipReason is not null).Select(s => (s.Order, s.SkipReason, s.SkippedBy, s.Branch)).Should().Equal(
            (3, "BranchNotChosen", _staff.Username, "Treatment"), (4, "BranchNotChosen", _staff.Username, "Treatment"));
    }

    private static Task<HttpResponseMessage> SkipAsync(HttpClient client, Guid pathwayId, Guid stageId, string reason, string? detail) =>
        client.PostAsJsonAsync($"{StageUrl(pathwayId, stageId)}/skip", new SkipPathwayStageRequest { Reason = reason, Detail = detail });

    private static Task<HttpResponseMessage> ReinstateAsync(HttpClient client, Guid pathwayId, Guid stageId) =>
        client.PostAsJsonAsync($"{StageUrl(pathwayId, stageId)}/reinstate",
            new ReinstatePathwayStageRequest { Reason = "Synthetic: needed after all." });

    private async Task<PagedResponse<PatientPathwaySummaryResponse>> SearchAsync(string query) =>
        await (await _admin.GetAsync($"/api/pathways/patient-pathways/search?{query}"))
            .ReadAsAsync<PagedResponse<PatientPathwaySummaryResponse>>();
}
