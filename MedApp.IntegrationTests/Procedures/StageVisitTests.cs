using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Contracts.Common;
using MedApp.Contracts.Procedures.Audit.Responses;
using MedApp.Contracts.Procedures.PatientProcedures.Requests;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.Contracts.Procedures.Timeline.Responses;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Procedures;

/// <summary>
/// Stages that involve a visit: the Requested appointment made when such a stage becomes current, the Required rule
/// (an attended appointment, or an admin's bypass with a reason), Suggested stages, the tidying up of Requested
/// appointments, and the audit and timeline entries each writes.
/// </summary>
[TestFixture]
public sealed class StageVisitTests : IntegrationTest
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
    public async Task CreateProcedure_RequiredStageFirst_CopiesTheRequirementAndRequestsItsVisit()
    {
        var procedure = await CreateProcedureWithStagesAsync("Required", "None");

        procedure.Stages.Select(s => s.VisitRequirement).Should().Equal("Required", "None");
        var requested = (await AppointmentsOfAsync(procedure.Id)).Should().ContainSingle().Subject;
        requested.Should().BeEquivalentTo(new
        {
            Status = "Requested",
            StageId = (Guid?)procedure.Stages[0].Id,
            StageName = procedure.Stages[0].Name,
            ProcedureTitle = procedure.Title,
            VisitRequirement = "Required",
            PatientId = procedure.PatientId,
            IsAutomatic = true,
        });
    }

    [Test]
    public async Task FinishStage_RequiredWithoutAVisit_Returns409ForStaffAndForAnAdminWithoutABody()
    {
        var procedure = await CreateProcedureWithStagesAsync("Required");
        var stage = procedure.Stages[0].Id;

        var staff = await FinishStageAsync(_staff.Client, procedure.Id, stage);
        var staffBypass = await FinishStageAsync(_staff.Client, procedure.Id, stage, new FinishProcedureStageRequest { WithoutVisit = true, Reason = WithoutVisitReason });
        var admin = await FinishStageAsync(_admin, procedure.Id, stage);
        var adminNoBypass = await FinishStageAsync(_admin, procedure.Id, stage, new FinishProcedureStageRequest { Reason = WithoutVisitReason });

        (await staff.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict)).Title.Should().Contain("admin");
        await staffBypass.ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await admin.ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await adminNoBypass.ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        (await GetProcedureAsync(procedure.Id)).Stages[0].State.Should().Be("NotStarted");
    }

    [Test]
    public async Task FinishStage_AdminBypassWithoutAReason_Returns400()
    {
        var procedure = await CreateProcedureWithStagesAsync("Required");

        var response = await FinishStageAsync(_admin, procedure.Id, procedure.Stages[0].Id,
            new FinishProcedureStageRequest { WithoutVisit = true, Reason = "  " });

        (await response.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest)).Errors.Should().ContainKey("Reason");
    }

    /// <summary>
    /// An admin finishes a Required stage without its visit, with a reason: the audit entry records it, the patient's
    /// timeline shows it (the reason only to admins), and the stage's Requested appointment is cancelled by the app.
    /// </summary>
    [Test]
    public async Task FinishStage_AdminBypassWithAReason_FinishesItAndRecordsWhy()
    {
        var procedure = await CreateProcedureWithStagesAsync("Required", "None");
        var stage = procedure.Stages[0];

        var finished = await (await FinishStageAsync(_admin, procedure.Id, stage.Id,
                new FinishProcedureStageRequest { WithoutVisit = true, Reason = WithoutVisitReason }))
            .ReadAsAsync<PatientProcedureResponse>();

        finished.Stages[0].State.Should().Be("Completed");
        finished.CurrentStageId.Should().Be(procedure.Stages[1].Id);

        var audit = (await AuditAsync(procedure.Id, "StageFinished")).Should().ContainSingle().Subject;
        audit.Details.Should().Contain("\"withoutVisit\":true")
            .And.Contain("\"visitRequirement\":\"Required\"")
            .And.Contain(WithoutVisitReason);

        var staffEntry = (await TimelineAsync(_staff.Client, procedure.PatientId, "Stages")).Single(e => e.Action == "StageFinished");
        staffEntry.Should().BeEquivalentTo(new { FinishedWithoutVisit = true, VisitRequirement = "Required", Reason = (string?)null });
        var adminEntry = (await TimelineAsync(_admin, procedure.PatientId, "Stages")).Single(e => e.Action == "StageFinished");
        adminEntry.Should().BeEquivalentTo(new { FinishedWithoutVisit = true, VisitRequirement = "Required", Reason = WithoutVisitReason, Username = AdminUsername });

        var cancelled = (await AppointmentsOfAsync(procedure.Id)).Should().ContainSingle().Subject;
        cancelled.Should().BeEquivalentTo(new { Status = "Cancelled", CancelReason = ProcedureChange.StageFinishedReason });
        var staffAppointments = await TimelineAsync(_staff.Client, procedure.PatientId, "Appointments");
        staffAppointments.Select(e => e.Action).Should().Equal("AppointmentCancelled", "AppointmentRequested");
        staffAppointments.Should().AllSatisfy(e => e.Reason.Should().BeNull());
        (await TimelineAsync(_admin, procedure.PatientId, "Appointments"))[0].Reason.Should().Be(ProcedureChange.StageFinishedReason);
    }

    [Test]
    public async Task FinishStage_RequiredAfterTheVisitWasAttended_StaffCanFinishIt()
    {
        var procedure = await CreateProcedureWithStagesAsync("Required");
        var stage = procedure.Stages[0].Id;
        var requested = (await AppointmentsOfAsync(procedure.Id)).Single();
        var start = NextBookableStart();

        await (await PostBookAsync(_staff.Client, requested.Id, _staff.UserId, start)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        // Booked isn't enough.
        await (await FinishStageAsync(_staff.Client, procedure.Id, stage)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);

        MoveClockPast(start);
        await (await PostOutcomeAsync(_staff.Client, requested.Id, "Attended")).ShouldHaveStatusAsync(HttpStatusCode.OK);

        var finished = await (await FinishStageAsync(_staff.Client, procedure.Id, stage)).ReadAsAsync<PatientProcedureResponse>();

        finished.Stages[0].State.Should().Be("Completed");
        finished.Status.Should().Be("InReview");
        (await AuditAsync(procedure.Id, "StageFinished")).Single().Details.Should().NotContain("withoutVisit");
        (await TimelineAsync(_staff.Client, procedure.PatientId, "Stages")).Single(e => e.Action == "StageFinished")
            .FinishedWithoutVisit.Should().BeFalse();
        (await GetAppointmentAsync(_staff.Client, requested.Id)).Status.Should().Be("Attended");
    }

    [Test]
    public async Task FinishStage_AttendedThenDidNotAttend_NeedsTheVisitAgain()
    {
        var procedure = await CreateProcedureWithStagesAsync("Required");
        var requested = (await AppointmentsOfAsync(procedure.Id)).Single();
        var start = NextBookableStart();
        await (await PostBookAsync(_staff.Client, requested.Id, _staff.UserId, start)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        MoveClockPast(start);
        await (await PostOutcomeAsync(_staff.Client, requested.Id, "Attended")).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await PostOutcomeAsync(_staff.Client, requested.Id, "DidNotAttend")).ShouldHaveStatusAsync(HttpStatusCode.OK);

        await (await FinishStageAsync(_staff.Client, procedure.Id, procedure.Stages[0].Id)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task FinishStage_SuggestedWithoutAVisit_StaffCanFinishItAndTheTimelineSaysSo()
    {
        var procedure = await CreateProcedureWithStagesAsync("Suggested");
        (await AppointmentsOfAsync(procedure.Id)).Should().ContainSingle().Which.VisitRequirement.Should().Be("Suggested");

        var finished = await (await FinishStageAsync(_staff.Client, procedure.Id, procedure.Stages[0].Id)).ReadAsAsync<PatientProcedureResponse>();

        finished.Stages[0].State.Should().Be("Completed");
        (await TimelineAsync(_staff.Client, procedure.PatientId, "Stages")).Single(e => e.Action == "StageFinished")
            .Should().BeEquivalentTo(new { FinishedWithoutVisit = true, VisitRequirement = "Suggested", Reason = (string?)null });
        (await AuditAsync(procedure.Id, "StageFinished")).Single().Details.Should().Contain("\"visitRequirement\":\"Suggested\"");
        (await AppointmentsOfAsync(procedure.Id)).Single().Status.Should().Be("Cancelled");
    }

    [Test]
    public async Task FinishStage_TheOneBeforeARequiredStage_RequestsThatStagesVisit()
    {
        var procedure = await CreateProcedureWithStagesAsync("None", "Required");
        (await AppointmentsOfAsync(procedure.Id)).Should().BeEmpty();

        await (await FinishStageAsync(_staff.Client, procedure.Id, procedure.Stages[0].Id)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        (await AppointmentsOfAsync(procedure.Id)).Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Status = "Requested", StageId = (Guid?)procedure.Stages[1].Id });
    }

    [Test]
    public async Task FinishStage_TheOneBeforeARequiredStageBookedAhead_DoesntRequestAnother()
    {
        var procedure = await CreateProcedureWithStagesAsync("None", "Required");
        var booked = await BookNewAppointmentAsync(_staff.Client, procedure.PatientId, _staff.UserId, NextBookableStart(),
            procedureId: procedure.Id, stageId: procedure.Stages[1].Id);

        await (await FinishStageAsync(_staff.Client, procedure.Id, procedure.Stages[0].Id)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        (await AppointmentsOfAsync(procedure.Id)).Select(a => a.Id).Should().Equal(booked.Id);
    }

    [Test]
    public async Task CreateAppointment_StageWithAnOpenVisit_Returns409()
    {
        var procedure = await CreateProcedureWithStagesAsync("Required");

        var response = await PostBookingAsync(_staff.Client, procedure.PatientId, _staff.UserId, NextBookableStart(),
            procedureId: procedure.Id, stageId: procedure.Stages[0].Id);

        await response.ShouldHaveStatusAsync(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task CancelProcedure_CancelsItsRequestedVisitsButNotBookedOnes()
    {
        var procedure = await CreateProcedureWithStagesAsync("Required", "Required");
        var requested = (await AppointmentsOfAsync(procedure.Id)).Single();
        var booked = await BookNewAppointmentAsync(_staff.Client, procedure.PatientId, _staff.UserId, NextBookableStart(),
            procedureId: procedure.Id, stageId: procedure.Stages[1].Id);

        await (await PostProcedureStatusAsync(_staff.Client, procedure.Id, "Cancelled", BlockReason)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        (await GetAppointmentAsync(_staff.Client, requested.Id))
            .Should().BeEquivalentTo(new { Status = "Cancelled", CancelReason = ProcedureChange.ProcedureCancelledReason });
        (await GetAppointmentAsync(_staff.Client, booked.Id)).Status.Should().Be("Booked");
    }

    [Test]
    public async Task BlockProcedure_ThenUnblock_KeepsTheRequestedVisitAndRequestsNoOther()
    {
        var procedure = await CreateProcedureWithStagesAsync("Required");

        await (await PostProcedureStatusAsync(_staff.Client, procedure.Id, "Blocked", BlockReason)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await PostProcedureStatusAsync(_staff.Client, procedure.Id, "Unassigned")).ShouldHaveStatusAsync(HttpStatusCode.OK);

        (await AppointmentsOfAsync(procedure.Id)).Should().ContainSingle().Which.Status.Should().Be("Requested");
    }

    [Test]
    public async Task FinishStage_AgainAfterAnAdminUnlockedIt_DoesntAskForTheVisitAgain()
    {
        var procedure = await CreateProcedureWithStagesAsync("Required");
        var stage = procedure.Stages[0].Id;
        await (await FinishStageAsync(_admin, procedure.Id, stage, new FinishProcedureStageRequest { WithoutVisit = true, Reason = WithoutVisitReason }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await _admin.PostAsync($"{StageUrl(procedure.Id, stage)}/unlock", null)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        var refinished = await FinishStageAsync(_staff.Client, procedure.Id, stage);

        (await refinished.ReadAsAsync<PatientProcedureResponse>()).Stages[0].State.Should().Be("Completed");
        (await AppointmentsOfAsync(procedure.Id)).Should().AllSatisfy(a => a.Status.Should().Be("Cancelled"));
    }

    /// <summary>Stages with no visit requirement behave exactly as before: finished with a POST with no body, no appointments.</summary>
    [Test]
    public async Task FinishStage_NoVisitRequirementAndNoBody_WorksAsBefore()
    {
        var procedure = await CreateProcedureWithStagesAsync("None", "None");

        await (await FinishStageAsync(_staff.Client, procedure.Id, procedure.Stages[0].Id)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        var finished = await (await FinishStageAsync(_staff.Client, procedure.Id, procedure.Stages[1].Id)).ReadAsAsync<PatientProcedureResponse>();

        finished.Status.Should().Be("InReview");
        finished.Stages.Should().AllSatisfy(s => s.VisitRequirement.Should().Be("None"));
        (await AppointmentsOfAsync(procedure.Id)).Should().BeEmpty();
        (await AuditAsync(procedure.Id, "StageFinished")).Should().HaveCount(2)
            .And.AllSatisfy(e => e.Details.Should().NotContain("withoutVisit"));
        (await TimelineAsync(_staff.Client, procedure.PatientId, "Stages")).Where(e => e.Action == "StageFinished")
            .Should().AllSatisfy(e => e.FinishedWithoutVisit.Should().BeFalse());
    }

    [Test]
    public async Task StageDefinition_VisitRequirement_IsSavedAndCanBeChanged()
    {
        var definition = await CreateStageDefinitionAsync(_admin, visitRequirement: "Suggested");
        definition.VisitRequirement.Should().Be("Suggested");

        var updated = await (await _admin.PatchAsJsonAsync(
                $"/api/procedures/patient-procedure-stage/definitions/{definition.Id}",
                new Contracts.Procedures.PatientProcedureStages.Definitions.Requests.UpdatePatientProcedureStageDefinitionRequest { VisitRequirement = "Required" }))
            .ReadAsAsync<Contracts.Procedures.PatientProcedureStages.Definitions.Responses.PatientProcedureStageDefinitionResponse>();

        updated.VisitRequirement.Should().Be("Required");
        await (await _admin.PostAsJsonAsync("/api/procedures/patient-procedure-stage/definitions",
                new Contracts.Procedures.PatientProcedureStages.Definitions.Requests.CreatePatientProcedureStageDefinitionRequest
                {
                    Name = UniqueName("Stage"),
                    Description = "Description",
                    Instructions = "Instructions",
                    VisitRequirement = "Sometimes",
                }))
            .ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
    }

    private async Task<PatientProcedureResponse> CreateProcedureWithStagesAsync(params string[] visitRequirements)
    {
        var ids = new List<Guid>();
        foreach (var requirement in visitRequirements)
            ids.Add((await CreateStageDefinitionAsync(_admin, visitRequirement: requirement)).Id);
        var template = await CreateTemplateAsync(_admin, [.. ids]);
        var patient = await CreatePatientAsync(_staff.Client);
        return await CreateProcedureAsync(_staff.Client, patient.Id, template.Id);
    }

    private async Task<PatientProcedureResponse> GetProcedureAsync(Guid procedureId) =>
        await (await _staff.Client.GetAsync(ProcedureUrl(procedureId))).ReadAsAsync<PatientProcedureResponse>();

    private Task<IReadOnlyList<AppointmentResponse>> AppointmentsOfAsync(Guid procedureId) =>
        SearchAppointmentsAsync(_staff.Client, $"procedureId={procedureId}");

    private async Task<IReadOnlyList<PatientProcedureAuditEntryResponse>> AuditAsync(Guid procedureId, string action) =>
        (await (await _admin.GetAsync($"/api/procedures/audit/search?procedureId={procedureId}&actions={action}&pageSize=100"))
            .ReadAsAsync<PagedResponse<PatientProcedureAuditEntryResponse>>()).Items;

    private static async Task<List<PatientTimelineEntryResponse>> TimelineAsync(HttpClient client, Guid patientId, string kind) =>
        (await (await client.GetAsync($"/api/patients/{patientId}/timeline?kinds={kind}&pageSize=100"))
            .ReadAsAsync<PatientTimelinePageResponse>()).Items;
}
