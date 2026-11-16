using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Common;
using MedApp.Contracts.Patients.Requests;
using MedApp.Contracts.Patients.Responses;
using MedApp.Contracts.Procedures.PatientProcedures.Requests;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.IntegrationTests.Common;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Privacy;

/// <summary>
/// A patient's privacy through the API, with real SQL: restricting a record, anonymising it (across every table the
/// timeline and the export read), the subject access export, and the retention review.
/// </summary>
[TestFixture]
public sealed class PatientPrivacyTests : IntegrationTest
{
    /// <summary>One string as all the free text a test writes, to look for after anonymising.</summary>
    private const string Canary = "privacy-canary-5b1e";

    private HttpClient _admin = null!;

    [SetUp]
    public async Task SignInAsync() => _admin = await SignInAsAdminAsync();

    private static string PatientUrl(Guid id) => $"/api/patients/{id}";

    private Task<HttpResponseMessage> SetRestrictionAsync(Guid patientId, bool set, string? reason) =>
        _admin.PutAsJsonAsync($"{PatientUrl(patientId)}/restriction", new SetPatientFlagRequest { Set = set, Reason = reason });

    [Test]
    public async Task Restriction_RefusesChangesToThePatientAndTheirProcedures_UntilLifted()
    {
        var procedure = await CreatePatientWithProcedureAsync(_admin);
        var staff = await CreateStaffAsync(UserRole);

        var restricted = await (await SetRestrictionAsync(procedure.PatientId, true, "Synthetic accuracy check")).ReadAsAsync<PatientResponse>();
        restricted.IsRestricted.Should().BeTrue();
        restricted.RestrictionReason.Should().Be("Synthetic accuracy check");

        (await staff.Client.PatchAsJsonAsync(PatientUrl(procedure.PatientId), new UpdatePatientRequest
            {
                FirstName = "Changed", LastName = "Changed", DateOfBirth = DateOfBirth, Email = $"changed@{PatientEmailDomain}",
            }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await PutStageNotesAsync(staff.Client, procedure.Id, procedure.Stages[0].Id, StageNotes)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var shown = await (await staff.Client.GetAsync(ProcedureUrl(procedure.Id))).ReadAsAsync<PatientProcedureResponse>();
        shown.ReadOnlyReason.Should().Be(PatientRules.RestrictedMessage);
        shown.CanWorkOnStages.Should().BeFalse();

        (await SetRestrictionAsync(procedure.PatientId, false, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PutStageNotesAsync(staff.Client, procedure.Id, procedure.Stages[0].Id, StageNotes)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Restriction_Staff_Is403()
    {
        var patient = await CreatePatientAsync(_admin);
        var staff = await CreateStaffAsync(UserRole);

        (await staff.Client.PutAsJsonAsync($"{PatientUrl(patient.Id)}/restriction", new SetPatientFlagRequest { Set = true, Reason = "No" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Anonymise_LeavesNoneOfTheFreeTextInTheRecordTimelineOrExport_AndRecordsWhy()
    {
        var patient = await CreatePatientAsync(_admin, firstName: "Canary", lastName: Canary);
        var template = await CreateTemplateWithStagesAsync(_admin, 1);
        var procedure = await CreateProcedureAsync(_admin, patient.Id, template.Id);
        (await PutStageNotesAsync(_admin, procedure.Id, procedure.Stages[0].Id, $"Notes about {Canary}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _admin.PatchAsJsonAsync(ProcedureUrl(procedure.Id), new UpdatePatientProcedureRequest
            {
                Title = $"Procedure for {Canary}",
                Notes = $"More about {Canary}",
                DueDate = UkToday.AddDays(7),
                Priority = "High",
            }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var anonymise = new AnonymisePatientRequest { Reason = "Synthetic: past its retention period", Confirmation = $"canary {Canary.ToUpperInvariant()}" };

        // Refused while the procedure is under way.
        (await _admin.PostAsJsonAsync($"{PatientUrl(patient.Id)}/anonymise", anonymise)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _admin.PostAsJsonAsync($"{ProcedureUrl(procedure.Id)}/status", new ChangePatientProcedureStatusRequest { Status = "Cancelled", Reason = $"Cancelled for {Canary}" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        // Refused without the patient's name.
        (await _admin.PostAsJsonAsync($"{PatientUrl(patient.Id)}/anonymise", new AnonymisePatientRequest { Reason = "Synthetic", Confirmation = "Someone Else" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var anonymised = await (await _admin.PostAsJsonAsync($"{PatientUrl(patient.Id)}/anonymise", anonymise)).ReadAsAsync<PatientResponse>();

        anonymised.IsAnonymised.Should().BeTrue();
        anonymised.FirstName.Should().Be(PatientAnonymiser.FirstName);
        anonymised.DateOfBirth.Should().Be(new DateOnly(DateOfBirth.Year, 1, 1));
        foreach (var url in new[]
                 {
                     PatientUrl(patient.Id),
                     ProcedureUrl(procedure.Id),
                     $"{PatientUrl(patient.Id)}/timeline",
                     $"{PatientUrl(patient.Id)}/timeline/overview",
                     $"/api/procedures/audit/search?PatientId={patient.Id}&PageSize=100",
                     $"{PatientUrl(patient.Id)}/export",
                 })
        {
            var body = await (await _admin.GetAsync(url)).ShouldHaveStatusAsync(HttpStatusCode.OK);
            (await body.Content.ReadAsStringAsync()).Should().NotContainEquivalentOf(Canary, url);
        }

        var log = await (await _admin.GetAsync($"/api/activity/search?PatientId={patient.Id}&Actions=PatientAnonymised"))
            .ReadAsAsync<PagedResponse<MedApp.Contracts.Activity.Responses.ActivityEntryResponse>>();
        log.Items.Should().ContainSingle().Which.Details.Should().Contain("Synthetic: past its retention period");
        (await _admin.PatchAsJsonAsync(PatientUrl(patient.Id), new UpdatePatientRequest
            {
                FirstName = "Back", LastName = "Again", DateOfBirth = DateOfBirth, Email = $"back@{PatientEmailDomain}",
            }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Export_HoldsTheWholeRecord_IsNeverCached_AndIsRecordedInItsOwnAccessLog()
    {
        var procedure = await CreatePatientWithProcedureAsync(_admin, stages: 2);
        (await PutStageNotesAsync(_admin, procedure.Id, procedure.Stages[0].Id, StageNotes)).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _admin.GetAsync($"{PatientUrl(procedure.PatientId)}/export");

        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var export = await response.ReadAsAsync<PatientExportResponse>();
        export.Patient.Id.Should().Be(procedure.PatientId);
        export.ExportedBy.Should().Be(AdminUsername);
        var exported = export.Procedures.Should().ContainSingle().Subject;
        exported.Stages.Should().HaveCount(2);
        exported.Stages[0].Notes.Should().Be(StageNotes);
        export.ProcedureHistory.Select(e => e.Action).Should().Contain(["Created", "StageNotesUpdated"]);
        export.AccessLog.Select(e => e.Action).Should().Contain(["PatientCreated", "PatientExported"]);
    }

    [Test]
    public async Task Export_Staff_Is403()
    {
        var patient = await CreatePatientAsync(_admin);
        var staff = await CreateStaffAsync(UserRole);

        (await staff.Client.GetAsync($"{PatientUrl(patient.Id)}/export")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task RetentionReview_ListsARecordPastItsPeriod_UntilAnAdminKeepsIt()
    {
        var due = await CreatePatientAsync(_admin);
        var recent = await CreatePatientAsync(_admin);
        // Last changed ten years ago: past an adult's eight.
        await ExecuteSqlAsync($"UPDATE Patients SET LastUpdated = DATEADD(year, -10, SYSUTCDATETIME()) WHERE Id = {due.Id}");

        var listed = await (await _admin.GetAsync("/api/patients/retention-review")).ReadAsAsync<PagedResponse<RetentionReviewItemResponse>>();
        var item = listed.Items.Should().ContainSingle().Subject;
        item.PatientId.Should().Be(due.Id);
        item.RetentionClass.Should().Be("Adult");
        listed.Items.Should().NotContain(i => i.PatientId == recent.Id);

        (await _admin.PostAsJsonAsync($"{PatientUrl(due.Id)}/retention/keep",
                new KeepPatientRecordRequest { KeepUntil = UkToday.AddYears(1), Reason = "Synthetic legal hold" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await (await _admin.GetAsync("/api/patients/retention-review")).ReadAsAsync<PagedResponse<RetentionReviewItemResponse>>())
            .Items.Should().BeEmpty();
        var kept = await (await _admin.GetAsync("/api/patients/retention-review?IncludeKept=true"))
            .ReadAsAsync<PagedResponse<RetentionReviewItemResponse>>();
        kept.Items.Should().ContainSingle().Which.KeptReason.Should().Be("Synthetic legal hold");
    }
}
