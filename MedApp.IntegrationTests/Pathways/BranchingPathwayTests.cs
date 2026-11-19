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
using MedApp.Contracts.Teams.Responses;
using MedApp.Domain.Teams;
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
    /// Results (a choice node: Benign leads to the Benign letter, Treatment to Treatment), Benign letter, Treatment and
    /// Follow-up (optional), then Discharge, which joins both routes.
    /// </summary>
    private async Task<PathwayTemplateResponse> CreateBranchingTemplateAsync()
    {
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
            ids.Add((await CreateStageDefinitionAsync(_admin)).Id);

        var response = await _admin.PostAsJsonAsync(TemplatesUrl, new CreatePathwayTemplateRequest
        {
            Name = UniqueName("Branching"),
            TeamId = Team.GeneralId,
            StageDefinitionIdsInOrder = ids,
            StageOptions =
            [
                new()
                {
                    ChoiceKind = "Assessment",
                    Outcomes = [new() { Name = "Benign", NextStageOrders = [2] }, new() { Name = "Treatment", NextStageOrders = [3] }]
                },
                new(),
                new(),
                new() { IsOptional = true },
                new(),
            ],
            Links =
            [
                new() { FromStageOrder = 1, ToStageOrder = 2 }, new() { FromStageOrder = 1, ToStageOrder = 3 },
                new() { FromStageOrder = 3, ToStageOrder = 4 }, new() { FromStageOrder = 2, ToStageOrder = 5 },
                new() { FromStageOrder = 4, ToStageOrder = 5 }
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
    public async Task Template_WithAChoiceNode_KeepsItsRoutesWhenAnEditLeavesThemOut()
    {
        var template = await CreateBranchingTemplateAsync();

        var read = await (await _admin.GetAsync($"{TemplatesUrl}/{template.Id}")).ReadAsAsync<PathwayTemplateResponse>();
        read.Stages.Select(s => (s.IsOptional, s.ChoiceKind)).Should().Equal(
            (false, "Assessment"), (false, null), (false, null), (true, null), (false, null));
        read.Links.Should().HaveCount(5);

        // A rename without the routes keeps them.
        var kept = await (await _admin.PatchAsJsonAsync($"{TemplatesUrl}/{template.Id}",
                new UpdatePathwayTemplateRequest { Name = UniqueName("Renamed") }))
            .ReadAsAsync<PathwayTemplateResponse>();
        kept.Links.Should().HaveCount(5);
        kept.Stages[0].Outcomes!.Select(o => o.Name).Should().Equal("Benign", "Treatment");
    }

    [Test]
    public async Task Template_AChoiceNodeWithOneOutcome_Returns400()
    {
        var first = await CreateStageDefinitionAsync(_admin);
        var second = await CreateStageDefinitionAsync(_admin);

        var response = await _admin.PostAsJsonAsync(TemplatesUrl, new CreatePathwayTemplateRequest
        {
            Name = UniqueName("Branching"),
            TeamId = Team.GeneralId,
            StageDefinitionIdsInOrder = [first.Id, second.Id],
            StageOptions = [new() { ChoiceKind = "Assessment", Outcomes = [new() { Name = "Benign", NextStageOrders = [2] }] }, new()],
            Links = [new() { FromStageOrder = 1, ToStageOrder = 2 }],
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task FinishChoiceNode_RecordingAnOutcome_GrowsThePathwayAndTheFilterAndTimelineFollow()
    {
        var pathway = await CreateBranchingPathwayAsync();
        pathway.Stages.Should().ContainSingle();
        var choice = pathway.Stages[0];
        choice.NeedsOutcome.Should().BeTrue();
        choice.Outcomes.Select(o => o.Name).Should().Equal("Benign", "Treatment");

        // No outcome is assumed: finishing without one is refused.
        await (await FinishStageAsync(_staff.Client, pathway.Id, choice.Id)).ShouldHaveStatusAsync(HttpStatusCode.BadRequest);

        var finished = await (await FinishStageAsync(_staff.Client, pathway.Id, choice.Id,
                new FinishPathwayStageRequest { Outcome = "Treatment" }))
            .ReadAsAsync<PatientPathwayResponse>();

        finished.Stages.Should().HaveCount(4);
        finished.Stages.Select(s => s.State).Should().Equal("Completed", "NotStarted", "NotStarted", "NotStarted");
        finished.CurrentStageId.Should().Be(finished.Stages[1].Id);

        // The stage reached, in SQL, through the stage links.
        (await SearchAsync($"StageDefinitionIds={finished.Stages[1].StageDefinitionId}")).Items.Should().ContainSingle(p => p.Id == pathway.Id);
        (await SearchAsync($"StageDefinitionIds={finished.Stages[3].StageDefinitionId}")).Items.Should().NotContain(p => p.Id == pathway.Id);

        var timeline = await (await _staff.Client.GetAsync($"/api/patients/{pathway.PatientId}/timeline?kinds=Stages"))
            .ReadAsAsync<PatientTimelinePageResponse>();
        timeline.Items.Single(e => e.Action == "BranchChosen").Branch.Should().Be("Treatment");
    }

    [Test]
    public async Task SkipAndReinstate_StaffSkipAnOptionalStageAndOnlyHoldersOfUnlockStagesReinstateIt()
    {
        var pathway = await CreateBranchingPathwayAsync();
        pathway = await (await FinishStageAsync(_staff.Client, pathway.Id, pathway.Stages[0].Id, new FinishPathwayStageRequest { Outcome = "Treatment" }))
            .ReadAsAsync<PatientPathwayResponse>();
        await (await FinishStageAsync(_staff.Client, pathway.Id, pathway.Stages[1].Id)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        var followUp = pathway.Stages[2];

        // Other needs a detail.
        await (await SkipAsync(_staff.Client, pathway.Id, followUp.Id, "Other", null)).ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
        var skipped = await (await SkipAsync(_staff.Client, pathway.Id, followUp.Id, "PatientDeclined", "Synthetic: declined by phone."))
            .ReadAsAsync<PatientPathwayResponse>();
        skipped.Stages[2].State.Should().Be("Skipped");
        skipped.Stages[2].SkipDetail.Should().Be("Synthetic: declined by phone.");
        skipped.CurrentStageId.Should().Be(pathway.Stages[3].Id);

        // A stage that isn't optional can't be skipped.
        await (await SkipAsync(_staff.Client, pathway.Id, pathway.Stages[3].Id, "NotNeeded", null)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);

        // Reinstating needs UnlockStages, which the built-in Admin role keeps.
        await (await ReinstateAsync(_staff.Client, pathway.Id, followUp.Id)).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        var reinstated = await (await ReinstateAsync(_admin, pathway.Id, followUp.Id)).ReadAsAsync<PatientPathwayResponse>();
        reinstated.CurrentStageId.Should().Be(followUp.Id);
        reinstated.Stages[2].State.Should().Be("NotStarted");

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
    public async Task Export_AChoiceNode_ListsItsRecordedOutcome()
    {
        var pathway = await CreateBranchingPathwayAsync();
        await (await FinishStageAsync(_staff.Client, pathway.Id, pathway.Stages[0].Id, new FinishPathwayStageRequest { Outcome = "Benign" }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        var export = await (await _admin.GetAsync($"/api/patients/{pathway.PatientId}/export")).ReadAsAsync<PatientExportResponse>();

        var stages = export.Pathways.Single().Stages;
        stages.Should().HaveCount(3);
        stages.Single(s => s.Order == 1).Branch.Should().Be("Benign");
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
