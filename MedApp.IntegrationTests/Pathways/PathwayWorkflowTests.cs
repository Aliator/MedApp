using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Contracts.Common;
using MedApp.Contracts.Pathways.Audit.Responses;
using MedApp.Contracts.Pathways.PatientPathways.Requests;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Contracts.Pathways.PatientPathwayStages.Definitions.Requests;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Pathways;

/// <summary>A pathway's life through the API: created from a template, its stages worked in order, its status, and
/// the audit log that records every change.</summary>
[TestFixture]
public sealed class PathwayWorkflowTests : IntegrationTest
{
    private HttpClient _admin = null!;
    private StaffAccount _staff = null!;

    [SetUp]
    public async Task SignInAsync()
    {
        _admin = await SignInAsAdminAsync();
        _staff = await CreateStaffAsync(UserRole);
    }

    [Test]
    public async Task CreatePathway_FromATemplate_CopiesItsStagesInOrder()
    {
        var first = await CreateStageDefinitionAsync(_admin);
        var second = await CreateStageDefinitionAsync(_admin);
        // A stage may repeat in a template.
        var template = await CreateTemplateAsync(_admin, first.Id, second.Id, first.Id);
        var patient = await CreatePatientAsync(_staff.Client);

        var pathway = await CreatePathwayAsync(_staff.Client, patient.Id, template.Id);

        pathway.Status.Should().Be("Unassigned");
        pathway.TemplateId.Should().Be(template.Id);
        pathway.TemplateName.Should().Be(template.Name);
        pathway.PatientName.Should().Contain(patient.LastName);
        pathway.Stages.Select(s => (s.StageOrder, s.StageDefinitionId, s.Name, s.Description, s.Instructions)).Should().Equal(
            (1, first.Id, first.Name, first.Description, first.Instructions),
            (2, second.Id, second.Name, second.Description, second.Instructions),
            (3, first.Id, first.Name, first.Description, first.Instructions));
        pathway.Stages.Should().AllSatisfy(s => s.State.Should().Be("NotStarted"));
        pathway.Stages.Select(s => s.Id).Should().OnlyHaveUniqueItems();
        pathway.CurrentStageId.Should().Be(pathway.Stages[0].Id);
        (await (await _admin.GetAsync($"/api/pathways/patient-pathway-stage/templates/{template.Id}"))
            .ReadAsAsync<Contracts.Pathways.PatientPathwayStages.Templates.Responses.PathwayTemplateResponse>())
            .PathwayCount.Should().Be(1);
    }

    [Test]
    public async Task CreatePathway_StageDefinitionChangedAfterwards_KeepsItsCopy()
    {
        var definition = await CreateStageDefinitionAsync(_admin);
        var template = await CreateTemplateAsync(_admin, definition.Id);
        var pathway = await CreatePathwayAsync(_staff.Client, (await CreatePatientAsync(_staff.Client)).Id, template.Id);

        await (await _admin.PatchAsJsonAsync(
                $"/api/pathways/patient-pathway-stage/definitions/{definition.Id}",
                new UpdatePatientPathwayStageDefinitionRequest { Name = UniqueName("Renamed"), Instructions = "Changed." }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        var reloaded = await GetAsync(pathway.Id);
        reloaded.Stages.Single().Name.Should().Be(definition.Name);
        reloaded.Stages.Single().Instructions.Should().Be(definition.Instructions);
    }

    [Test]
    public async Task CreatePathway_UnknownPatient_Returns404()
    {
        var template = await CreateTemplateWithStagesAsync(_admin, 1);

        var response = await _staff.Client.PostAsJsonAsync("/api/pathways/patient-pathways", new CreatePatientPathwayRequest
        {
            PatientId = Guid.NewGuid(),
            TemplateId = template.Id,
            DueDate = UkToday,
            Priority = "Low",
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task WorkStages_InOrder_MovesThePathwayThroughItsStatuses()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin, stages: 3);
        var (s1, s2, s3) = (pathway.Stages[0].Id, pathway.Stages[1].Id, pathway.Stages[2].Id);
        var client = _staff.Client;

        // Notes on the first stage start it, and the pathway.
        var started = await (await PutStageNotesAsync(client, pathway.Id, s1, StageNotes)).ReadAsAsync<PatientPathwayResponse>();
        started.Status.Should().Be("InProgress");
        started.Stages[0].Should().BeEquivalentTo(new { State = "InProgress", Notes = StageNotes, IsLocked = false, CanEdit = true });
        started.Stages[0].StartedByUsername.Should().Be(_staff.Username);

        // Finishing it locks it, and the next stage is current.
        var finished = await (await FinishStageAsync(client, pathway.Id, s1)).ReadAsAsync<PatientPathwayResponse>();
        finished.Stages[0].Should().BeEquivalentTo(new { State = "Completed", IsLocked = true, CanEdit = false });
        finished.Stages[0].CompletedByUsername.Should().Be(_staff.Username);
        finished.CurrentStageId.Should().Be(s2);
        finished.CompletedStageCount.Should().Be(1);

        await FinishStageAsync(client, pathway.Id, s2);
        var inReview = await (await FinishStageAsync(client, pathway.Id, s3)).ReadAsAsync<PatientPathwayResponse>();
        inReview.Status.Should().Be("InReview");
        inReview.CurrentStageId.Should().BeNull();
        inReview.AllowedStatusChanges.Should().Contain("Completed");

        var completed = await ChangeStatusAsync(client, pathway.Id, "Completed");
        completed.Status.Should().Be("Completed");
        completed.CanWorkOnStages.Should().BeFalse();
    }

    [Test]
    public async Task UpdateStageNotes_NotTheCurrentStage_Returns409()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin, stages: 2);

        var response = await PutStageNotesAsync(_staff.Client, pathway.Id, pathway.Stages[1].Id, StageNotes);

        var problem = await response.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict);
        problem.Title.Should().Contain("in order");
        (await GetAsync(pathway.Id)).Stages[1].Notes.Should().BeEmpty();
    }

    [Test]
    public async Task UpdateStageNotes_FinishedStage_Returns409UntilAnAdminUnlocksIt()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin, stages: 2);
        var stage = pathway.Stages[0].Id;
        await (await FinishStageAsync(_staff.Client, pathway.Id, stage)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        await (await PutStageNotesAsync(_staff.Client, pathway.Id, stage, StageNotes)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await (await _staff.Client.PostAsync($"{StageUrl(pathway.Id, stage)}/unlock", null)).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);

        var unlocked = await (await (await ClinicalAsync(_admin)).PostAsync($"{StageUrl(pathway.Id, stage)}/unlock", null)).ReadAsAsync<PatientPathwayResponse>();
        unlocked.Stages[0].Should().BeEquivalentTo(new { State = "Completed", IsLocked = false, CanEdit = true });

        var edited = await (await PutStageNotesAsync(_staff.Client, pathway.Id, stage, StageNotes)).ReadAsAsync<PatientPathwayResponse>();
        edited.Stages[0].Notes.Should().Be(StageNotes);
    }

    [Test]
    public async Task UnlockStage_InReviewPathway_MovesItBackToInProgress()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin, stages: 1);
        var stage = pathway.Stages[0].Id;
        (await (await FinishStageAsync(_staff.Client, pathway.Id, stage)).ReadAsAsync<PatientPathwayResponse>())
            .Status.Should().Be("InReview");

        var unlocked = await (await (await ClinicalAsync(_admin)).PostAsync($"{StageUrl(pathway.Id, stage)}/unlock", null)).ReadAsAsync<PatientPathwayResponse>();

        unlocked.Status.Should().Be("InProgress");
        var refinished = await (await FinishStageAsync(_staff.Client, pathway.Id, stage)).ReadAsAsync<PatientPathwayResponse>();
        refinished.Status.Should().Be("InReview");
    }

    [Test]
    public async Task ChangeStatus_CompletedBeforeEveryStageIsFinished_Returns409()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin, stages: 1);

        var response = await PostStatusAsync(_staff.Client, pathway.Id, "Completed");

        await response.ShouldHaveStatusAsync(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task ChangeStatus_BlockWithoutAReason_Returns400ValidationProblem()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin);

        var response = await PostStatusAsync(_staff.Client, pathway.Id, "Blocked");

        var problem = await response.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainKey("Reason");
    }

    [Test]
    public async Task ChangeStatus_BlockedThenUnblocked_ReturnsToWhereItWasAndStopsWorkMeanwhile()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin);
        await PutStageNotesAsync(_staff.Client, pathway.Id, pathway.Stages[0].Id, StageNotes);

        var blocked = await ChangeStatusAsync(_staff.Client, pathway.Id, "Blocked", BlockReason);
        blocked.Status.Should().Be("Blocked");
        blocked.BlockedFromStatus.Should().Be("InProgress");
        blocked.AllowedStatusChanges.Should().BeEquivalentTo(["InProgress", "Cancelled"]);
        await (await FinishStageAsync(_staff.Client, pathway.Id, pathway.Stages[0].Id)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);

        var unblocked = await ChangeStatusAsync(_staff.Client, pathway.Id, "InProgress");
        unblocked.Status.Should().Be("InProgress");
        unblocked.BlockedFromStatus.Should().BeNull();
    }

    [Test]
    public async Task DeletePathway_Cancelled_DeletesItAndKeepsItsAuditHistory()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin);
        await (await (await ClinicalAsync(_admin)).DeleteAsync(PathwayUrl(pathway.Id))).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await ChangeStatusAsync(_staff.Client, pathway.Id, "Cancelled", BlockReason);
        await (await _staff.Client.DeleteAsync(PathwayUrl(pathway.Id))).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);

        await (await (await ClinicalAsync(_admin)).DeleteAsync(PathwayUrl(pathway.Id))).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await (await _staff.Client.GetAsync(PathwayUrl(pathway.Id))).ShouldHaveStatusAsync(HttpStatusCode.NotFound);
        var audit = await AuditAsync(pathway.Id);
        audit.Select(e => e.Action).Should().BeEquivalentTo(["Created", "StatusChanged", "Deleted"]);
        // The patient no longer has a pathway, so can be deleted.
        await (await _staff.Client.DeleteAsync($"/api/patients/{pathway.PatientId}")).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task EveryChange_IsAuditedWithWhoDidIt()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin, stages: 2);
        var (s1, s2) = (pathway.Stages[0].Id, pathway.Stages[1].Id);
        var client = _staff.Client;

        await PutStageNotesAsync(client, pathway.Id, s1, StageNotes);       // StageStarted, StatusChanged, StageNotesUpdated
        await FinishStageAsync(client, pathway.Id, s1);                     // StageFinished
        await FinishStageAsync(client, pathway.Id, s2);                     // StageStarted, StageFinished, StatusChanged
        await (await ClinicalAsync(_admin)).PostAsync($"{StageUrl(pathway.Id, s1)}/unlock", null); // StageUnlocked, StatusChanged
        await (await client.PatchAsJsonAsync(PathwayUrl(pathway.Id),
            new UpdatePatientPathwayRequest { Priority = "Urgent" })).ShouldHaveStatusAsync(HttpStatusCode.OK); // DetailsUpdated

        var audit = await AuditAsync(pathway.Id);

        audit.Select(e => e.Action).Should().BeEquivalentTo([
            "Created",
            "StageStarted", "StatusChanged", "StageNotesUpdated",
            "StageFinished",
            "StageStarted", "StageFinished", "StatusChanged",
            "StageUnlocked", "StatusChanged",
            "DetailsUpdated",
        ]);
        audit.Should().AllSatisfy(e =>
        {
            e.PatientPathwayId.Should().Be(pathway.Id);
            e.PatientId.Should().Be(pathway.PatientId);
        });
        audit.Where(e => e.Action == "StageUnlocked").Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Username = (await ClinicStaffAsync()).Username, StageId = (Guid?)s1 });
        audit.Where(e => e.Action == "StageNotesUpdated").Should().ContainSingle()
            .Which.Username.Should().Be(_staff.Username);
        var statusChanges = audit.Where(e => e.Action == "StatusChanged").Select(e => e.Details).ToList();
        statusChanges.Should().Contain(d => d.Contains("\"to\":\"InProgress\"") && d.Contains("\"from\":\"Unassigned\""));
        statusChanges.Should().Contain(d => d.Contains("\"to\":\"InReview\""));
        statusChanges.Should().Contain(d => d.Contains("\"from\":\"InReview\"") && d.Contains("\"to\":\"InProgress\""));
    }

    [Test]
    public async Task SearchAudit_ByAction_FiltersInSql()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin, stages: 1);
        await FinishStageAsync(_staff.Client, pathway.Id, pathway.Stages[0].Id);

        var page = await (await _admin.GetAsync($"/api/pathways/audit/search?pathwayId={pathway.Id}&actions=StageFinished&actions=Created"))
            .ReadAsAsync<PagedResponse<PatientPathwayAuditEntryResponse>>();

        page.Items.Select(e => e.Action).Should().BeEquivalentTo(["Created", "StageFinished"]);
    }

    private async Task<PatientPathwayResponse> GetAsync(Guid pathwayId) =>
        await (await _staff.Client.GetAsync(PathwayUrl(pathwayId))).ReadAsAsync<PatientPathwayResponse>();

    private static Task<HttpResponseMessage> PostStatusAsync(HttpClient client, Guid pathwayId, string status, string? reason = null) =>
        client.PostAsJsonAsync($"{PathwayUrl(pathwayId)}/status", new ChangePatientPathwayStatusRequest { Status = status, Reason = reason });

    private static async Task<PatientPathwayResponse> ChangeStatusAsync(HttpClient client, Guid pathwayId, string status, string? reason = null) =>
        await (await PostStatusAsync(client, pathwayId, status, reason)).ReadAsAsync<PatientPathwayResponse>();

    private async Task<IReadOnlyList<PatientPathwayAuditEntryResponse>> AuditAsync(Guid pathwayId) =>
        (await (await _admin.GetAsync($"/api/pathways/audit/search?pathwayId={pathwayId}&pageSize=100"))
            .ReadAsAsync<PagedResponse<PatientPathwayAuditEntryResponse>>()).Items;
}
