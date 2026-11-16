using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Contracts.Common;
using MedApp.Contracts.Procedures.Audit.Responses;
using MedApp.Contracts.Procedures.PatientProcedures.Requests;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Requests;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Procedures;

/// <summary>A procedure's life through the API: created from a template, its stages worked in order, its status, and
/// the audit log that records every change.</summary>
[TestFixture]
public sealed class ProcedureWorkflowTests : IntegrationTest
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
    public async Task CreateProcedure_FromATemplate_CopiesItsStagesInOrder()
    {
        var first = await CreateStageDefinitionAsync(_admin);
        var second = await CreateStageDefinitionAsync(_admin);
        // A stage may repeat in a template.
        var template = await CreateTemplateAsync(_admin, first.Id, second.Id, first.Id);
        var patient = await CreatePatientAsync(_staff.Client);

        var procedure = await CreateProcedureAsync(_staff.Client, patient.Id, template.Id);

        procedure.Status.Should().Be("Unassigned");
        procedure.TemplateId.Should().Be(template.Id);
        procedure.TemplateName.Should().Be(template.Name);
        procedure.PatientName.Should().Contain(patient.LastName);
        procedure.Stages.Select(s => (s.StageOrder, s.StageDefinitionId, s.Name, s.Description, s.Instructions)).Should().Equal(
            (1, first.Id, first.Name, first.Description, first.Instructions),
            (2, second.Id, second.Name, second.Description, second.Instructions),
            (3, first.Id, first.Name, first.Description, first.Instructions));
        procedure.Stages.Should().AllSatisfy(s => s.State.Should().Be("NotStarted"));
        procedure.Stages.Select(s => s.Id).Should().OnlyHaveUniqueItems();
        procedure.CurrentStageId.Should().Be(procedure.Stages[0].Id);
        (await (await _admin.GetAsync($"/api/procedures/patient-procedure-stage/templates/{template.Id}"))
            .ReadAsAsync<Contracts.Procedures.PatientProcedureStages.Templates.Responses.PatientProcedureStageTemplateResponse>())
            .ProcedureCount.Should().Be(1);
    }

    [Test]
    public async Task CreateProcedure_StageDefinitionChangedAfterwards_KeepsItsCopy()
    {
        var definition = await CreateStageDefinitionAsync(_admin);
        var template = await CreateTemplateAsync(_admin, definition.Id);
        var procedure = await CreateProcedureAsync(_staff.Client, (await CreatePatientAsync(_staff.Client)).Id, template.Id);

        await (await _admin.PatchAsJsonAsync(
                $"/api/procedures/patient-procedure-stage/definitions/{definition.Id}",
                new UpdatePatientProcedureStageDefinitionRequest { Name = UniqueName("Renamed"), Instructions = "Changed." }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        var reloaded = await GetAsync(procedure.Id);
        reloaded.Stages.Single().Name.Should().Be(definition.Name);
        reloaded.Stages.Single().Instructions.Should().Be(definition.Instructions);
    }

    [Test]
    public async Task CreateProcedure_UnknownPatient_Returns404()
    {
        var template = await CreateTemplateWithStagesAsync(_admin, 1);

        var response = await _staff.Client.PostAsJsonAsync("/api/procedures/patient-procedures", new CreatePatientProcedureRequest
        {
            PatientId = Guid.NewGuid(),
            TemplateId = template.Id,
            DueDate = UkToday,
            Priority = "Low",
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task WorkStages_InOrder_MovesTheProcedureThroughItsStatuses()
    {
        var procedure = await CreatePatientWithProcedureAsync(_admin, stages: 3);
        var (s1, s2, s3) = (procedure.Stages[0].Id, procedure.Stages[1].Id, procedure.Stages[2].Id);
        var client = _staff.Client;

        // Notes on the first stage start it, and the procedure.
        var started = await (await PutStageNotesAsync(client, procedure.Id, s1, StageNotes)).ReadAsAsync<PatientProcedureResponse>();
        started.Status.Should().Be("InProgress");
        started.Stages[0].Should().BeEquivalentTo(new { State = "InProgress", Notes = StageNotes, IsLocked = false, CanEdit = true });
        started.Stages[0].StartedByUsername.Should().Be(_staff.Username);

        // Finishing it locks it, and the next stage is current.
        var finished = await (await FinishStageAsync(client, procedure.Id, s1)).ReadAsAsync<PatientProcedureResponse>();
        finished.Stages[0].Should().BeEquivalentTo(new { State = "Completed", IsLocked = true, CanEdit = false });
        finished.Stages[0].CompletedByUsername.Should().Be(_staff.Username);
        finished.CurrentStageId.Should().Be(s2);
        finished.CompletedStageCount.Should().Be(1);

        await FinishStageAsync(client, procedure.Id, s2);
        var inReview = await (await FinishStageAsync(client, procedure.Id, s3)).ReadAsAsync<PatientProcedureResponse>();
        inReview.Status.Should().Be("InReview");
        inReview.CurrentStageId.Should().BeNull();
        inReview.AllowedStatusChanges.Should().Contain("Completed");

        var completed = await ChangeStatusAsync(client, procedure.Id, "Completed");
        completed.Status.Should().Be("Completed");
        completed.CanWorkOnStages.Should().BeFalse();
    }

    [Test]
    public async Task UpdateStageNotes_NotTheCurrentStage_Returns409()
    {
        var procedure = await CreatePatientWithProcedureAsync(_admin, stages: 2);

        var response = await PutStageNotesAsync(_staff.Client, procedure.Id, procedure.Stages[1].Id, StageNotes);

        var problem = await response.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict);
        problem.Title.Should().Contain("in order");
        (await GetAsync(procedure.Id)).Stages[1].Notes.Should().BeEmpty();
    }

    [Test]
    public async Task UpdateStageNotes_FinishedStage_Returns409UntilAnAdminUnlocksIt()
    {
        var procedure = await CreatePatientWithProcedureAsync(_admin, stages: 2);
        var stage = procedure.Stages[0].Id;
        await (await FinishStageAsync(_staff.Client, procedure.Id, stage)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        await (await PutStageNotesAsync(_staff.Client, procedure.Id, stage, StageNotes)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await (await _staff.Client.PostAsync($"{StageUrl(procedure.Id, stage)}/unlock", null)).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);

        var unlocked = await (await _admin.PostAsync($"{StageUrl(procedure.Id, stage)}/unlock", null)).ReadAsAsync<PatientProcedureResponse>();
        unlocked.Stages[0].Should().BeEquivalentTo(new { State = "Completed", IsLocked = false, CanEdit = true });

        var edited = await (await PutStageNotesAsync(_staff.Client, procedure.Id, stage, StageNotes)).ReadAsAsync<PatientProcedureResponse>();
        edited.Stages[0].Notes.Should().Be(StageNotes);
    }

    [Test]
    public async Task UnlockStage_InReviewProcedure_MovesItBackToInProgress()
    {
        var procedure = await CreatePatientWithProcedureAsync(_admin, stages: 1);
        var stage = procedure.Stages[0].Id;
        (await (await FinishStageAsync(_staff.Client, procedure.Id, stage)).ReadAsAsync<PatientProcedureResponse>())
            .Status.Should().Be("InReview");

        var unlocked = await (await _admin.PostAsync($"{StageUrl(procedure.Id, stage)}/unlock", null)).ReadAsAsync<PatientProcedureResponse>();

        unlocked.Status.Should().Be("InProgress");
        var refinished = await (await FinishStageAsync(_staff.Client, procedure.Id, stage)).ReadAsAsync<PatientProcedureResponse>();
        refinished.Status.Should().Be("InReview");
    }

    [Test]
    public async Task ChangeStatus_CompletedBeforeEveryStageIsFinished_Returns409()
    {
        var procedure = await CreatePatientWithProcedureAsync(_admin, stages: 1);

        var response = await PostStatusAsync(_staff.Client, procedure.Id, "Completed");

        await response.ShouldHaveStatusAsync(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task ChangeStatus_BlockWithoutAReason_Returns400ValidationProblem()
    {
        var procedure = await CreatePatientWithProcedureAsync(_admin);

        var response = await PostStatusAsync(_staff.Client, procedure.Id, "Blocked");

        var problem = await response.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainKey("Reason");
    }

    [Test]
    public async Task ChangeStatus_BlockedThenUnblocked_ReturnsToWhereItWasAndStopsWorkMeanwhile()
    {
        var procedure = await CreatePatientWithProcedureAsync(_admin);
        await PutStageNotesAsync(_staff.Client, procedure.Id, procedure.Stages[0].Id, StageNotes);

        var blocked = await ChangeStatusAsync(_staff.Client, procedure.Id, "Blocked", BlockReason);
        blocked.Status.Should().Be("Blocked");
        blocked.BlockedFromStatus.Should().Be("InProgress");
        blocked.AllowedStatusChanges.Should().BeEquivalentTo(["InProgress", "Cancelled"]);
        await (await FinishStageAsync(_staff.Client, procedure.Id, procedure.Stages[0].Id)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);

        var unblocked = await ChangeStatusAsync(_staff.Client, procedure.Id, "InProgress");
        unblocked.Status.Should().Be("InProgress");
        unblocked.BlockedFromStatus.Should().BeNull();
    }

    [Test]
    public async Task DeleteProcedure_Cancelled_DeletesItAndKeepsItsAuditHistory()
    {
        var procedure = await CreatePatientWithProcedureAsync(_admin);
        await (await _admin.DeleteAsync(ProcedureUrl(procedure.Id))).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await ChangeStatusAsync(_staff.Client, procedure.Id, "Cancelled", BlockReason);
        await (await _staff.Client.DeleteAsync(ProcedureUrl(procedure.Id))).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);

        await (await _admin.DeleteAsync(ProcedureUrl(procedure.Id))).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await (await _staff.Client.GetAsync(ProcedureUrl(procedure.Id))).ShouldHaveStatusAsync(HttpStatusCode.NotFound);
        var audit = await AuditAsync(procedure.Id);
        audit.Select(e => e.Action).Should().BeEquivalentTo(["Created", "StatusChanged", "Deleted"]);
        // The patient no longer has a procedure, so can be deleted.
        await (await _staff.Client.DeleteAsync($"/api/patients/{procedure.PatientId}")).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task EveryChange_IsAuditedWithWhoDidIt()
    {
        var procedure = await CreatePatientWithProcedureAsync(_admin, stages: 2);
        var (s1, s2) = (procedure.Stages[0].Id, procedure.Stages[1].Id);
        var client = _staff.Client;

        await PutStageNotesAsync(client, procedure.Id, s1, StageNotes);       // StageStarted, StatusChanged, StageNotesUpdated
        await FinishStageAsync(client, procedure.Id, s1);                     // StageFinished
        await FinishStageAsync(client, procedure.Id, s2);                     // StageStarted, StageFinished, StatusChanged
        await _admin.PostAsync($"{StageUrl(procedure.Id, s1)}/unlock", null); // StageUnlocked, StatusChanged
        await (await client.PatchAsJsonAsync(ProcedureUrl(procedure.Id),
            new UpdatePatientProcedureRequest { Priority = "Urgent" })).ShouldHaveStatusAsync(HttpStatusCode.OK); // DetailsUpdated

        var audit = await AuditAsync(procedure.Id);

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
            e.PatientProcedureId.Should().Be(procedure.Id);
            e.PatientId.Should().Be(procedure.PatientId);
        });
        audit.Where(e => e.Action == "StageUnlocked").Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Username = AdminUsername, StageId = (Guid?)s1 });
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
        var procedure = await CreatePatientWithProcedureAsync(_admin, stages: 1);
        await FinishStageAsync(_staff.Client, procedure.Id, procedure.Stages[0].Id);

        var page = await (await _admin.GetAsync($"/api/procedures/audit/search?procedureId={procedure.Id}&actions=StageFinished&actions=Created"))
            .ReadAsAsync<PagedResponse<PatientProcedureAuditEntryResponse>>();

        page.Items.Select(e => e.Action).Should().BeEquivalentTo(["Created", "StageFinished"]);
    }

    private async Task<PatientProcedureResponse> GetAsync(Guid procedureId) =>
        await (await _staff.Client.GetAsync(ProcedureUrl(procedureId))).ReadAsAsync<PatientProcedureResponse>();

    private static Task<HttpResponseMessage> PostStatusAsync(HttpClient client, Guid procedureId, string status, string? reason = null) =>
        client.PostAsJsonAsync($"{ProcedureUrl(procedureId)}/status", new ChangePatientProcedureStatusRequest { Status = status, Reason = reason });

    private static async Task<PatientProcedureResponse> ChangeStatusAsync(HttpClient client, Guid procedureId, string status, string? reason = null) =>
        await (await PostStatusAsync(client, procedureId, status, reason)).ReadAsAsync<PatientProcedureResponse>();

    private async Task<IReadOnlyList<PatientProcedureAuditEntryResponse>> AuditAsync(Guid procedureId) =>
        (await (await _admin.GetAsync($"/api/procedures/audit/search?procedureId={procedureId}&pageSize=100"))
            .ReadAsAsync<PagedResponse<PatientProcedureAuditEntryResponse>>()).Items;
}
