using System.Net;
using FluentAssertions;
using MedApp.Contracts.Activity.Responses;
using MedApp.Contracts.Common;
using MedApp.Contracts.Patients.Requests;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Privacy;

/// <summary>
/// Every endpoint that reads, and what it records in the activity log (the DPIA's risk 1 rests on this list). Each GET
/// endpoint the API maps must be in <see cref="Coverage"/>: one that returns a patient's data names the action it
/// records, and one that doesn't says why it records nothing. A new GET endpoint fails here until it's classified.
/// </summary>
[TestFixture]
public sealed class EndpointCoverageTests : IntegrationTest
{
    /// <summary>Returns patient data, so records this action against each patient it returns.</summary>
    private static Coverage Records(string action) => new(action, null);

    /// <summary>Records nothing, for this reason.</summary>
    private static Coverage Nothing(string why) => new(null, why);

    private sealed record Coverage(string? Action, string? Why);

    /// <summary>Every GET endpoint (and the page fallback), by route pattern.</summary>
    private static readonly Dictionary<string, Coverage> CoverageTable = new()
    {
        // Patients and their records: every read of patient data is recorded.
        ["api/patients/{id:guid}"] = Records("PatientViewed"),
        ["api/patients/search"] = Records("PatientListed"),
        ["api/patients"] = Records("PatientsSearched"), // one entry, filters ["all"] (only the deprecated client calls it)
        ["api/patients/retention-review"] = Records("PatientListed"),
        ["api/patients/{id:guid}/export"] = Records("PatientExported"),
        ["api/patients/{id:guid}/timeline"] = Records("PatientViewed"),
        ["api/patients/{id:guid}/timeline/overview"] = Records("PatientViewed"),
        ["api/procedures/patient-procedures/{id:guid}"] = Records("ProcedureViewed"),
        ["api/procedures/patient-procedures/search"] = Records("PatientListed"),
        ["api/fhir/Patient/{id}"] = Records("PatientViewed"),
        ["api/fhir/Patient"] = Records("PatientListed"), // and one PatientsSearched entry, via "fhir"

        // Labels for the side nav's pins: names of records the user already opened and chose to pin, not a read.
        ["api/patients/labels"] = Nothing("labels for pins the user chose; recording them would log views nobody made"),
        ["api/procedures/patient-procedures/labels"] = Nothing("labels for pins the user chose; recording them would log views nobody made"),

        // Admins' own audit reading: the logs themselves.
        ["api/procedures/audit/search"] = Nothing("the admin-only procedure audit log"),
        ["api/activity/search"] = Nothing("the admin-only activity log itself"),
        ["api/procedures/insights"] = Nothing("counts, not patient details"),

        // The user's own data and accounts: no patient data.
        ["api/notifications"] = Nothing("the user's own notifications"),
        ["api/auth/session"] = Nothing("the user's own session"),
        ["api/auth/whoami"] = Nothing("the user's own identity"),
        ["api/auth/me/settings"] = Nothing("the user's own settings"),
        ["api/auth/me/email"] = Nothing("the user's own email address"),
        ["api/auth/me/two-factor"] = Nothing("the user's own two-factor state"),
        ["api/auth/features"] = Nothing("whether this deployment sends email; asked before signing in"),
        ["api/auth/security-settings"] = Nothing("the admins' sign-in rules: configuration"),
        ["api/auth/users"] = Nothing("staff accounts, not patients"),
        ["api/auth/users/search"] = Nothing("staff accounts, not patients"),
        ["api/auth/users/{username}"] = Nothing("staff accounts, not patients"),
        ["api/auth/users/{username}/roles"] = Nothing("staff accounts, not patients"),
        ["api/auth/roles"] = Nothing("role names"),
        ["api/demographics/status"] = Nothing("whether the demographics lookup is set up (the lookup itself is a POST, recorded as DemographicsLookedUp)"),

        // Configuration.
        ["api/procedures/patient-procedure-stage/definitions"] = Nothing("stage definitions: configuration"),
        ["api/procedures/patient-procedure-stage/definitions/{id:guid}"] = Nothing("stage definitions: configuration"),
        ["api/procedures/patient-procedure-stage/templates"] = Nothing("templates: configuration"),
        ["api/procedures/patient-procedure-stage/templates/{id:guid}"] = Nothing("templates: configuration"),

        // Infrastructure.
        ["/api/notifications/hub"] = Nothing("the live notifications connection (it carries notifications already stored)"),
        ["/api/notifications/hub/negotiate"] = Nothing("the live notifications connection"),
        ["{*path:nonfile}"] = Nothing("the app's pages; their data comes from the endpoints above"),
        ["/api/{**path}"] = Nothing("an /api path that matches no endpoint: a 404"),
        ["api/fhir/metadata"] = Nothing("the FHIR API's CapabilityStatement: what it can do, no patient data"),
        ["/api/fhir/{**path}"] = Nothing("a FHIR path that matches no endpoint: a 404 OperationOutcome"),
        ["/health"] = Nothing("health check"),
        ["/alive"] = Nothing("health check"),
    };

    [Test]
    public void EveryGetEndpoint_IsClassified()
    {
        var mapped = Api.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<IHttpMethodMetadata>() is not { } methods || methods.HttpMethods.Contains("GET"))
            .Select(e => e.RoutePattern.RawText ?? string.Empty)
            .Distinct()
            .ToList();

        mapped.Should().Contain("api/patients/{id:guid}", "the scan must see the controllers' endpoints");
        mapped.Where(pattern => !CoverageTable.ContainsKey(pattern)).Should().BeEmpty(
            "every GET endpoint must say what it records in the activity log (add it to the table)");
    }

    [Test]
    public async Task EveryReadOfPatientData_RecordsItsAction()
    {
        var admin = await SignInAsAdminAsync();
        var adminId = await GetUserIdAsync(admin, AdminUsername);
        var procedure = await CreatePatientWithProcedureAsync(admin);
        var patientId = procedure.PatientId;
        // Last changed ten years ago, so it's on the retention review.
        var old = await CreatePatientAsync(admin);
        await ExecuteSqlAsync($"UPDATE Patients SET LastUpdated = DATEADD(year, -10, SYSUTCDATETIME()) WHERE Id = {old.Id}");
        // A synthetic NHS number (the 999 range), so the FHIR search finds them.
        await ExecuteSqlAsync($"UPDATE Patients SET NhsNumber = '9990000018' WHERE Id = {patientId}");

        var calls = new (string Url, string Action, Guid? PatientId)[]
        {
            ($"/api/patients/{patientId}", "PatientViewed", patientId),
            ("/api/patients/search", "PatientListed", patientId),
            ("/api/patients", "PatientsSearched", null),
            ("/api/patients/retention-review", "PatientListed", old.Id),
            ($"/api/patients/{patientId}/export", "PatientExported", patientId),
            ($"/api/patients/{patientId}/timeline", "PatientViewed", patientId),
            ($"/api/patients/{patientId}/timeline/overview", "PatientViewed", patientId),
            ($"/api/procedures/patient-procedures/{procedure.Id}", "ProcedureViewed", patientId),
            ("/api/procedures/patient-procedures/search", "PatientListed", patientId),
            ($"/api/fhir/Patient/{patientId}", "PatientViewed", patientId),
            ("/api/fhir/Patient?identifier=https%3A%2F%2Ffhir.nhs.uk%2FId%2Fnhs-number%7C9990000018", "PatientListed", patientId),
        };
        calls.Select(c => c.Action).Distinct().Should().BeEquivalentTo(
            CoverageTable.Values.Select(c => c.Action).OfType<string>().Distinct(), "each recorded action is called here");

        foreach (var (url, action, expectedPatient) in calls)
        {
            await ExecuteSqlAsync($"DELETE FROM ActivityEntries");

            (await admin.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK, url);

            var entries = (await (await admin.GetAsync($"/api/activity/search?PageSize=100&UserId={adminId}&Actions={action}"))
                .ReadAsAsync<PagedResponse<ActivityEntryResponse>>()).Items;
            entries.Should().Contain(e => e.PatientId == expectedPatient, url);
        }
    }

    [Test]
    public async Task AllPatients_RecordsOneSearchForAll_NotAnEntryPerPatient()
    {
        var admin = await SignInAsAdminAsync();
        await CreatePatientAsync(admin);
        await CreatePatientAsync(admin);
        await ExecuteSqlAsync($"DELETE FROM ActivityEntries");

        (await admin.GetAsync("/api/patients")).StatusCode.Should().Be(HttpStatusCode.OK);

        var entries = (await (await admin.GetAsync("/api/activity/search?PageSize=100&Actions=PatientsSearched&Actions=PatientListed"))
            .ReadAsAsync<PagedResponse<ActivityEntryResponse>>()).Items;
        var entry = entries.Should().ContainSingle().Subject;
        entry.Action.Should().Be("PatientsSearched");
        entry.Details.Should().Contain("\"filters\":[\"all\"]").And.Contain("\"resultCount\":2");
    }
}
