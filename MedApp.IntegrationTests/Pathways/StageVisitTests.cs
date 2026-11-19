using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Contracts.Common;
using MedApp.Contracts.Pathways.Audit.Responses;
using MedApp.Contracts.Pathways.PatientPathways.Requests;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Contracts.Pathways.Timeline.Responses;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Pathways;

/// <summary>
/// Stages that involve a visit: the Requested appointment made when such a stage becomes current, the Required rule
/// (an attended appointment, or the bypass with a reason by a holder of FinishWithoutVisit, such as a senior
/// clinician; the built-in Admin role hasn't it), Suggested stages, the tidying up of Requested appointments, and the
/// audit and timeline entries each writes.
/// </summary>
[TestFixture]
public sealed class StageVisitTests : IntegrationTest
{
    private HttpClient _admin = null!;
    private StaffAccount _staff = null!;
    private StaffAccount _senior = null!;

    [SetUp]
    public async Task SignInAsync()
    {
        _admin = await SignInAsAdminAsync();
        _staff = await CreateStaffAsync(UserRole);
        _senior = await CreateStaffAsync(SeniorClinicianRole);
    }

    [Test]
    public async Task CreatePathway_RequiredStageFirst_CopiesTheRequirementAndRequestsItsVisit()
    {
        var pathway = await CreatePathwayWithStagesAsync("Required", "None");

        pathway.Stages.Select(s => s.VisitRequirement).Should().Equal("Required", "None");
        var requested = (await AppointmentsOfAsync(pathway.Id)).Should().ContainSingle().Subject;
        requested.Should().BeEquivalentTo(new
        {
            Status = "Requested",
            StageId = (Guid?)pathway.Stages[0].Id,
            StageName = pathway.Stages[0].Name,
            PathwayTitle = pathway.Title,
            VisitRequirement = "Required",
            PatientId = pathway.PatientId,
            IsAutomatic = true,
        });
    }

    [Test]
    public async Task FinishStage_RequiredWithoutAVisit_Returns409ForStaff_403ForAnAdmin_AndWithoutTheBypassBody()
    {
        var pathway = await CreatePathwayWithStagesAsync("Required");
        var stage = pathway.Stages[0].Id;

        var staff = await FinishStageAsync(_staff.Client, pathway.Id, stage);
        var staffBypass = await FinishStageAsync(_staff.Client, pathway.Id, stage, new FinishPathwayStageRequest { WithoutVisit = true, Reason = WithoutVisitReason });
        // Straight to the API (the helper does an admin's clinical work as staff): admins are read only.
        var admin = await _admin.PostAsync($"{StageUrl(pathway.Id, stage)}/finish", null);
        var adminBypass = await _admin.PostAsJsonAsync($"{StageUrl(pathway.Id, stage)}/finish", new FinishPathwayStageRequest { WithoutVisit = true, Reason = WithoutVisitReason });
        var seniorNoBypass = await FinishStageAsync(_senior.Client, pathway.Id, stage, new FinishPathwayStageRequest { Reason = WithoutVisitReason });

        (await staff.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict)).Title.Should().Contain("senior clinician");
        await staffBypass.ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await admin.ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await adminBypass.ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await seniorNoBypass.ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        (await GetPathwayAsync(pathway.Id)).Stages[0].State.Should().Be("NotStarted");
    }

    [Test]
    public async Task FinishStage_BypassWithoutAReason_Returns400()
    {
        var pathway = await CreatePathwayWithStagesAsync("Required");

        var response = await FinishStageAsync(_senior.Client, pathway.Id, pathway.Stages[0].Id,
            new FinishPathwayStageRequest { WithoutVisit = true, Reason = "  " });

        (await response.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest)).Errors.Should().ContainKey("Reason");
    }

    /// <summary>
    /// A senior clinician finishes a Required stage without its visit, with a reason: the audit entry records it, the patient's
    /// timeline shows it (the reason only to admins), and the stage's Requested appointment is cancelled by the app.
    /// </summary>
    [Test]
    public async Task FinishStage_SeniorClinicianBypassWithAReason_FinishesItAndRecordsWhy()
    {
        var pathway = await CreatePathwayWithStagesAsync("Required", "None");
        var stage = pathway.Stages[0];

        var finished = await (await FinishStageAsync(_senior.Client, pathway.Id, stage.Id,
                new FinishPathwayStageRequest { WithoutVisit = true, Reason = WithoutVisitReason }))
            .ReadAsAsync<PatientPathwayResponse>();

        finished.Stages[0].State.Should().Be("Completed");
        finished.CurrentStageId.Should().Be(pathway.Stages[1].Id);

        var audit = (await AuditAsync(pathway.Id, "StageFinished")).Should().ContainSingle().Subject;
        audit.Details.Should().Contain("\"withoutVisit\":true")
            .And.Contain("\"visitRequirement\":\"Required\"")
            .And.Contain(WithoutVisitReason);

        var staffEntry = (await TimelineAsync(_staff.Client, pathway.PatientId, "Stages")).Single(e => e.Action == "StageFinished");
        staffEntry.Should().BeEquivalentTo(new { FinishedWithoutVisit = true, VisitRequirement = "Required", Reason = (string?)null });
        var adminEntry = (await TimelineAsync(_admin, pathway.PatientId, "Stages")).Single(e => e.Action == "StageFinished");
        adminEntry.Should().BeEquivalentTo(new { FinishedWithoutVisit = true, VisitRequirement = "Required", Reason = WithoutVisitReason, Username = _senior.Username });

        var cancelled = (await AppointmentsOfAsync(pathway.Id)).Should().ContainSingle().Subject;
        cancelled.Should().BeEquivalentTo(new { Status = "Cancelled", CancelReason = PathwayChange.StageFinishedReason });
        var staffAppointments = await TimelineAsync(_staff.Client, pathway.PatientId, "Appointments");
        staffAppointments.Select(e => e.Action).Should().Equal("AppointmentCancelled", "AppointmentRequested");
        staffAppointments.Should().AllSatisfy(e => e.Reason.Should().BeNull());
        (await TimelineAsync(_admin, pathway.PatientId, "Appointments"))[0].Reason.Should().Be(PathwayChange.StageFinishedReason);
    }

    [Test]
    public async Task FinishStage_RequiredAfterTheVisitWasAttended_StaffCanFinishIt()
    {
        var pathway = await CreatePathwayWithStagesAsync("Required");
        var stage = pathway.Stages[0].Id;
        var requested = (await AppointmentsOfAsync(pathway.Id)).Single();
        var start = NextBookableStart();

        await (await PostBookAsync(_staff.Client, requested.Id, _staff.UserId, start)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        // Booked isn't enough.
        await (await FinishStageAsync(_staff.Client, pathway.Id, stage)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);

        MoveClockPast(start);
        await (await PostOutcomeAsync(_staff.Client, requested.Id, "Attended")).ShouldHaveStatusAsync(HttpStatusCode.OK);

        var finished = await (await FinishStageAsync(_staff.Client, pathway.Id, stage)).ReadAsAsync<PatientPathwayResponse>();

        finished.Stages[0].State.Should().Be("Completed");
        finished.Status.Should().Be("InReview");
        (await AuditAsync(pathway.Id, "StageFinished")).Single().Details.Should().NotContain("withoutVisit");
        (await TimelineAsync(_staff.Client, pathway.PatientId, "Stages")).Single(e => e.Action == "StageFinished")
            .FinishedWithoutVisit.Should().BeFalse();
        (await GetAppointmentAsync(_staff.Client, requested.Id)).Status.Should().Be("Attended");
    }

    [Test]
    public async Task FinishStage_AttendedThenDidNotAttend_NeedsTheVisitAgain()
    {
        var pathway = await CreatePathwayWithStagesAsync("Required");
        var requested = (await AppointmentsOfAsync(pathway.Id)).Single();
        var start = NextBookableStart();
        await (await PostBookAsync(_staff.Client, requested.Id, _staff.UserId, start)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        MoveClockPast(start);
        await (await PostOutcomeAsync(_staff.Client, requested.Id, "Attended")).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await PostOutcomeAsync(_staff.Client, requested.Id, "DidNotAttend")).ShouldHaveStatusAsync(HttpStatusCode.OK);

        await (await FinishStageAsync(_staff.Client, pathway.Id, pathway.Stages[0].Id)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task FinishStage_SuggestedWithoutAVisit_StaffCanFinishItAndTheTimelineSaysSo()
    {
        var pathway = await CreatePathwayWithStagesAsync("Suggested");
        (await AppointmentsOfAsync(pathway.Id)).Should().ContainSingle().Which.VisitRequirement.Should().Be("Suggested");

        var finished = await (await FinishStageAsync(_staff.Client, pathway.Id, pathway.Stages[0].Id)).ReadAsAsync<PatientPathwayResponse>();

        finished.Stages[0].State.Should().Be("Completed");
        (await TimelineAsync(_staff.Client, pathway.PatientId, "Stages")).Single(e => e.Action == "StageFinished")
            .Should().BeEquivalentTo(new { FinishedWithoutVisit = true, VisitRequirement = "Suggested", Reason = (string?)null });
        (await AuditAsync(pathway.Id, "StageFinished")).Single().Details.Should().Contain("\"visitRequirement\":\"Suggested\"");
        (await AppointmentsOfAsync(pathway.Id)).Single().Status.Should().Be("Cancelled");
    }

    [Test]
    public async Task FinishStage_TheOneBeforeARequiredStage_RequestsThatStagesVisit()
    {
        var pathway = await CreatePathwayWithStagesAsync("None", "Required");
        (await AppointmentsOfAsync(pathway.Id)).Should().BeEmpty();

        await (await FinishStageAsync(_staff.Client, pathway.Id, pathway.Stages[0].Id)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        (await AppointmentsOfAsync(pathway.Id)).Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Status = "Requested", StageId = (Guid?)pathway.Stages[1].Id });
    }

    [Test]
    public async Task FinishStage_TheOneBeforeARequiredStageBookedAhead_DoesntRequestAnother()
    {
        var pathway = await CreatePathwayWithStagesAsync("None", "Required");
        var booked = await BookNewAppointmentAsync(_staff.Client, pathway.PatientId, _staff.UserId, NextBookableStart(),
            pathwayId: pathway.Id, stageId: pathway.Stages[1].Id);

        await (await FinishStageAsync(_staff.Client, pathway.Id, pathway.Stages[0].Id)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        (await AppointmentsOfAsync(pathway.Id)).Select(a => a.Id).Should().Equal(booked.Id);
    }

    [Test]
    public async Task CreateAppointment_StageWithAnOpenVisit_Returns409()
    {
        var pathway = await CreatePathwayWithStagesAsync("Required");

        var response = await PostBookingAsync(_staff.Client, pathway.PatientId, _staff.UserId, NextBookableStart(),
            pathwayId: pathway.Id, stageId: pathway.Stages[0].Id);

        await response.ShouldHaveStatusAsync(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task CancelPathway_CancelsItsRequestedVisitsButNotBookedOnes()
    {
        var pathway = await CreatePathwayWithStagesAsync("Required", "Required");
        var requested = (await AppointmentsOfAsync(pathway.Id)).Single();
        var booked = await BookNewAppointmentAsync(_staff.Client, pathway.PatientId, _staff.UserId, NextBookableStart(),
            pathwayId: pathway.Id, stageId: pathway.Stages[1].Id);

        await (await PostPathwayStatusAsync(_staff.Client, pathway.Id, "Cancelled", BlockReason)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        (await GetAppointmentAsync(_staff.Client, requested.Id))
            .Should().BeEquivalentTo(new { Status = "Cancelled", CancelReason = PathwayChange.PathwayCancelledReason });
        (await GetAppointmentAsync(_staff.Client, booked.Id)).Status.Should().Be("Booked");
    }

    [Test]
    public async Task BlockPathway_ThenUnblock_KeepsTheRequestedVisitAndRequestsNoOther()
    {
        var pathway = await CreatePathwayWithStagesAsync("Required");

        await (await PostPathwayStatusAsync(_staff.Client, pathway.Id, "Blocked", BlockReason)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await PostPathwayStatusAsync(_staff.Client, pathway.Id, "Unassigned")).ShouldHaveStatusAsync(HttpStatusCode.OK);

        (await AppointmentsOfAsync(pathway.Id)).Should().ContainSingle().Which.Status.Should().Be("Requested");
    }

    [Test]
    public async Task FinishStage_AgainAfterAnAdminUnlockedIt_DoesntAskForTheVisitAgain()
    {
        var pathway = await CreatePathwayWithStagesAsync("Required");
        var stage = pathway.Stages[0].Id;
        await (await FinishStageAsync(_senior.Client, pathway.Id, stage, new FinishPathwayStageRequest { WithoutVisit = true, Reason = WithoutVisitReason }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await (await ClinicalAsync(_admin)).PostAsync($"{StageUrl(pathway.Id, stage)}/unlock", null)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        var refinished = await FinishStageAsync(_staff.Client, pathway.Id, stage);

        (await refinished.ReadAsAsync<PatientPathwayResponse>()).Stages[0].State.Should().Be("Completed");
        (await AppointmentsOfAsync(pathway.Id)).Should().AllSatisfy(a => a.Status.Should().Be("Cancelled"));
    }

    /// <summary>Stages with no visit requirement behave exactly as before: finished with a POST with no body, no appointments.</summary>
    [Test]
    public async Task FinishStage_NoVisitRequirementAndNoBody_WorksAsBefore()
    {
        var pathway = await CreatePathwayWithStagesAsync("None", "None");

        await (await FinishStageAsync(_staff.Client, pathway.Id, pathway.Stages[0].Id)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        var finished = await (await FinishStageAsync(_staff.Client, pathway.Id, pathway.Stages[1].Id)).ReadAsAsync<PatientPathwayResponse>();

        finished.Status.Should().Be("InReview");
        finished.Stages.Should().AllSatisfy(s => s.VisitRequirement.Should().Be("None"));
        (await AppointmentsOfAsync(pathway.Id)).Should().BeEmpty();
        (await AuditAsync(pathway.Id, "StageFinished")).Should().HaveCount(2)
            .And.AllSatisfy(e => e.Details.Should().NotContain("withoutVisit"));
        (await TimelineAsync(_staff.Client, pathway.PatientId, "Stages")).Where(e => e.Action == "StageFinished")
            .Should().AllSatisfy(e => e.FinishedWithoutVisit.Should().BeFalse());
    }

    [Test]
    public async Task StageDefinition_VisitRequirement_IsSavedAndCanBeChanged()
    {
        var definition = await CreateStageDefinitionAsync(_admin, visitRequirement: "Suggested");
        definition.VisitRequirement.Should().Be("Suggested");

        var updated = await (await _admin.PatchAsJsonAsync(
                $"/api/pathways/patient-pathway-stage/definitions/{definition.Id}",
                new Contracts.Pathways.PatientPathwayStages.Definitions.Requests.UpdatePatientPathwayStageDefinitionRequest { VisitRequirement = "Required" }))
            .ReadAsAsync<Contracts.Pathways.PatientPathwayStages.Definitions.Responses.PatientPathwayStageDefinitionResponse>();

        updated.VisitRequirement.Should().Be("Required");
        await (await _admin.PostAsJsonAsync("/api/pathways/patient-pathway-stage/definitions",
                new Contracts.Pathways.PatientPathwayStages.Definitions.Requests.CreatePatientPathwayStageDefinitionRequest
                {
                    Name = UniqueName("Stage"),
                    Description = "Description",
                    Instructions = "Instructions",
                    VisitRequirement = "Sometimes",
                }))
            .ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
    }

    private async Task<PatientPathwayResponse> CreatePathwayWithStagesAsync(params string[] visitRequirements)
    {
        var ids = new List<Guid>();
        foreach (var requirement in visitRequirements)
            ids.Add((await CreateStageDefinitionAsync(_admin, visitRequirement: requirement)).Id);
        var template = await CreateTemplateAsync(_admin, [.. ids]);
        var patient = await CreatePatientAsync(_staff.Client);
        return await CreatePathwayAsync(_staff.Client, patient.Id, template.Id);
    }

    private async Task<PatientPathwayResponse> GetPathwayAsync(Guid pathwayId) =>
        await (await _staff.Client.GetAsync(PathwayUrl(pathwayId))).ReadAsAsync<PatientPathwayResponse>();

    private Task<IReadOnlyList<AppointmentResponse>> AppointmentsOfAsync(Guid pathwayId) =>
        SearchAppointmentsAsync(_staff.Client, $"pathwayId={pathwayId}");

    private async Task<IReadOnlyList<PatientPathwayAuditEntryResponse>> AuditAsync(Guid pathwayId, string action) =>
        (await (await _admin.GetAsync($"/api/pathways/audit/search?pathwayId={pathwayId}&actions={action}&pageSize=100"))
            .ReadAsAsync<PagedResponse<PatientPathwayAuditEntryResponse>>()).Items;

    private static async Task<List<PatientTimelineEntryResponse>> TimelineAsync(HttpClient client, Guid patientId, string kind) =>
        (await (await client.GetAsync($"/api/patients/{patientId}/timeline?kinds={kind}&pageSize=100"))
            .ReadAsAsync<PatientTimelinePageResponse>()).Items;
}
