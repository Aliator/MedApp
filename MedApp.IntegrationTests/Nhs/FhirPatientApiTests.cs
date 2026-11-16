using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MedApp.Contracts.Activity.Responses;
using MedApp.Contracts.Common;
using MedApp.Contracts.Patients.Requests;
using MedApp.Contracts.Patients.Responses;
using MedApp.IntegrationTests.Common;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Nhs;

/// <summary>
/// The read-only FHIR API through HTTP: the UK Core Patient and the searchset Bundle as FHIR JSON, OperationOutcomes
/// for errors (authorisation included), no caching, and the activity log.
/// </summary>
[TestFixture]
public sealed class FhirPatientApiTests : IntegrationTest
{
    private const string NhsNumber = "9990000034";
    private const string NhsSystem = "https://fhir.nhs.uk/Id/nhs-number";

    private HttpClient _staff = null!;
    private PatientResponse _patient = null!;

    [SetUp]
    public async Task SetUpPatientAsync()
    {
        _staff = (await CreateStaffAsync(UserRole)).Client;
        _patient = await (await _staff.PostAsJsonAsync("/api/patients", new CreatePatientRequest
        {
            FirstName = "Cara",
            LastName = UniqueName("Fhir"),
            DateOfBirth = DateOfBirth,
            Email = $"cara@{PatientEmailDomain}",
            NhsNumber = NhsNumber,
        })).ReadAsAsync<PatientResponse>(HttpStatusCode.Created);
    }

    private static async Task<JsonElement> FhirJsonAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        await response.ShouldHaveStatusAsync(expected);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/fhir+json");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    [Test]
    public async Task ReadPatient_Known_ReturnsAUkCorePatient()
    {
        var json = await FhirJsonAsync(await _staff.GetAsync($"/api/fhir/Patient/{_patient.Id}"), HttpStatusCode.OK);

        json.EnumerateObject().First().Name.Should().Be("resourceType");
        json.GetProperty("resourceType").GetString().Should().Be("Patient");
        json.GetProperty("id").GetString().Should().Be(_patient.Id.ToString());
        json.GetProperty("meta").GetProperty("profile")[0].GetString()
            .Should().Be("https://fhir.hl7.org.uk/StructureDefinition/UKCore-Patient");
        var identifier = json.GetProperty("identifier")[0];
        identifier.GetProperty("system").GetString().Should().Be(NhsSystem);
        identifier.GetProperty("value").GetString().Should().Be(NhsNumber);
        identifier.GetProperty("extension")[0].GetProperty("valueCodeableConcept").GetProperty("coding")[0]
            .GetProperty("code").GetString().Should().Be("02");
        json.GetProperty("name")[0].GetProperty("family").GetString().Should().Be(_patient.LastName);
        json.GetProperty("birthDate").GetString().Should().Be(DateOfBirth.ToString("yyyy-MM-dd"));
        json.GetRawText().Should().NotContain(":null", "FHIR JSON never holds a null");
    }

    [Test]
    public async Task ReadPatient_Unknown_Returns404OperationOutcome()
    {
        foreach (var id in new[] { Guid.NewGuid().ToString(), "not-a-guid" })
        {
            var json = await FhirJsonAsync(await _staff.GetAsync($"/api/fhir/Patient/{id}"), HttpStatusCode.NotFound);

            json.GetProperty("resourceType").GetString().Should().Be("OperationOutcome");
            json.GetProperty("issue")[0].GetProperty("code").GetString().Should().Be("not-found");
        }
    }

    [Test]
    public async Task SearchPatients_ByNhsNumber_ReturnsASearchsetBundle()
    {
        var identifier = Uri.EscapeDataString($"{NhsSystem}|{NhsNumber}");

        var json = await FhirJsonAsync(await _staff.GetAsync($"/api/fhir/Patient?identifier={identifier}"), HttpStatusCode.OK);

        json.GetProperty("resourceType").GetString().Should().Be("Bundle");
        json.GetProperty("type").GetString().Should().Be("searchset");
        json.GetProperty("total").GetInt32().Should().Be(1);
        var entry = json.GetProperty("entry")[0];
        entry.GetProperty("fullUrl").GetString().Should().Be($"https://localhost/api/fhir/Patient/{_patient.Id}");
        entry.GetProperty("resource").GetProperty("id").GetString().Should().Be(_patient.Id.ToString());
        entry.GetProperty("search").GetProperty("mode").GetString().Should().Be("match");
    }

    [Test]
    public async Task SearchPatients_NobodyOrAnotherSystem_ReturnsAnEmptyBundle()
    {
        foreach (var identifier in new[] { $"{NhsSystem}|9990000042", "https://example.org/mrn|123" })
        {
            var json = await FhirJsonAsync(
                await _staff.GetAsync($"/api/fhir/Patient?identifier={Uri.EscapeDataString(identifier)}"), HttpStatusCode.OK);

            json.GetProperty("total").GetInt32().Should().Be(0);
            json.TryGetProperty("entry", out _).Should().BeFalse();
        }
    }

    [Test]
    public async Task SearchPatients_NoOrInvalidIdentifier_Returns400OperationOutcome()
    {
        foreach (var query in new[] { "", $"?identifier={Uri.EscapeDataString($"{NhsSystem}|9990000035")}" })
        {
            var json = await FhirJsonAsync(await _staff.GetAsync($"/api/fhir/Patient{query}"), HttpStatusCode.BadRequest);

            var issue = json.GetProperty("issue")[0];
            issue.GetProperty("severity").GetString().Should().Be("error");
            issue.GetProperty("code").GetString().Should().Be("invalid");
            issue.GetProperty("expression")[0].GetString().Should().Be("identifier");
        }
    }

    [Test]
    public async Task Fhir_NotSignedIn_Returns401OperationOutcome()
    {
        var json = await FhirJsonAsync(await NewClient().GetAsync($"/api/fhir/Patient/{_patient.Id}"), HttpStatusCode.Unauthorized);

        json.GetProperty("issue")[0].GetProperty("code").GetString().Should().Be("login");
    }

    [Test]
    public async Task Fhir_SignedInWithoutAStaffRole_Returns403OperationOutcome()
    {
        // An account given a role (so it can choose its password), then left with none.
        var account = await CreateStaffAsync(UserRole);
        var admin = await SignInAsAdminAsync();
        (await admin.PutAsJsonAsync($"/api/auth/users/{account.Username}/roles", new MedApp.Contracts.Authentication.Requests.SetUserRolesRequest([])))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var json = await FhirJsonAsync(await account.Client.GetAsync($"/api/fhir/Patient/{_patient.Id}"), HttpStatusCode.Forbidden);

        json.GetProperty("issue")[0].GetProperty("code").GetString().Should().Be("forbidden");
    }

    [Test]
    public async Task ReadAndSearch_AreInTheActivityLog_WithoutTheNumber()
    {
        await (await _staff.GetAsync($"/api/fhir/Patient/{_patient.Id}")).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await _staff.GetAsync($"/api/fhir/Patient?identifier={Uri.EscapeDataString($"{NhsSystem}|{NhsNumber}")}"))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        var admin = await SignInAsAdminAsync();
        var log = (await (await admin.GetAsync($"/api/activity/search?PageSize=100&PatientId={_patient.Id}"))
            .ReadAsAsync<PagedResponse<ActivityEntryResponse>>()).Items;
        var searches = (await (await admin.GetAsync("/api/activity/search?PageSize=100&Actions=PatientsSearched"))
            .ReadAsAsync<PagedResponse<ActivityEntryResponse>>()).Items;

        log.Select(e => e.Action).Should().Contain(["PatientViewed", "PatientListed"]);
        searches.Should().ContainSingle(e => e.Details.Contains("\"via\":\"fhir\""))
            .Which.Details.Should().Contain("\"resultCount\":1").And.NotContain(NhsNumber);
    }
}
