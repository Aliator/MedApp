using System.Net;
using FluentAssertions;
using MedApp.Contracts.Activity.Responses;
using MedApp.Contracts.Common;
using MedApp.Contracts.Patients.Responses;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.IntegrationTests.Common;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Privacy;

/// <summary>
/// The side nav's pin labels record nothing (they aren't views), while procedure searches and a patient's timeline
/// record the patients they show, once per person every few minutes.
/// </summary>
[TestFixture]
public sealed class ProcedureReadsAndLabelsTests : IntegrationTest
{
    private HttpClient _admin = null!;

    [SetUp]
    public async Task SignInAsync() => _admin = await SignInAsAdminAsync();

    private async Task<List<ActivityEntryResponse>> ActivityOfAsync(Guid userId) =>
        (await (await _admin.GetAsync($"/api/activity/search?PageSize=100&UserId={userId}"))
            .ReadAsAsync<PagedResponse<ActivityEntryResponse>>()).Items.ToList();

    [Test]
    public async Task Labels_ReturnNamesAndTitles_LeaveOutGoneOnes_AndRecordNothing()
    {
        var staff = await CreateStaffAsync(UserRole);
        var procedure = await CreatePatientWithProcedureAsync(_admin);
        var patient = await (await _admin.GetAsync($"/api/patients/{procedure.PatientId}")).ReadAsAsync<PatientResponse>();
        var gone = Guid.NewGuid();

        var patients = await (await staff.Client.GetAsync($"/api/patients/labels?ids={procedure.PatientId}&ids={gone}"))
            .ReadAsAsync<List<PatientLabelResponse>>();
        var procedures = await (await staff.Client.GetAsync($"/api/procedures/patient-procedures/labels?ids={gone}&ids={procedure.Id}"))
            .ReadAsAsync<List<PatientProcedureLabelResponse>>();

        patients.Should().Equal(new PatientLabelResponse(procedure.PatientId, $"{patient.FirstName} {patient.LastName}"));
        procedures.Should().Equal(new PatientProcedureLabelResponse(procedure.Id, procedure.Title, $"{patient.FirstName} {patient.LastName}"));
        (await ActivityOfAsync(staff.UserId)).Select(e => e.Action)
            .Should().NotContain(["PatientViewed", "ProcedureViewed", "PatientListed"]);
    }

    [Test]
    public async Task Labels_MoreThanTwentyIds_Is400()
    {
        var ids = string.Join("&", Enumerable.Range(0, 21).Select(_ => $"ids={Guid.NewGuid()}"));

        (await _admin.GetAsync($"/api/patients/labels?{ids}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _admin.GetAsync($"/api/procedures/patient-procedures/labels?{ids}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Labels_NoIds_ReturnNone()
    {
        (await (await _admin.GetAsync("/api/patients/labels")).ReadAsAsync<List<PatientLabelResponse>>()).Should().BeEmpty();
    }

    [Test]
    public async Task ProcedureSearch_RecordsEachPatientOnThePage_OnceEveryFewMinutes()
    {
        var staff = await CreateStaffAsync(UserRole);
        var first = await CreatePatientWithProcedureAsync(_admin);
        var second = await CreatePatientWithProcedureAsync(_admin);

        (await staff.Client.GetAsync("/api/procedures/patient-procedures/search")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await staff.Client.GetAsync("/api/procedures/patient-procedures/search")).StatusCode.Should().Be(HttpStatusCode.OK);

        var listed = (await ActivityOfAsync(staff.UserId)).Where(e => e.Action == "PatientListed").ToList();
        listed.Select(e => e.PatientId).Should().BeEquivalentTo([first.PatientId, second.PatientId]);
    }

    [Test]
    public async Task Timeline_AndItsOverview_RecordOneViewOfThePatient()
    {
        var staff = await CreateStaffAsync(UserRole);
        var procedure = await CreatePatientWithProcedureAsync(_admin);

        (await staff.Client.GetAsync($"/api/patients/{procedure.PatientId}/timeline")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await staff.Client.GetAsync($"/api/patients/{procedure.PatientId}/timeline/overview")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await ActivityOfAsync(staff.UserId)).Where(e => e.Action == "PatientViewed")
            .Should().ContainSingle().Which.PatientId.Should().Be(procedure.PatientId);
    }
}
