using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Contracts.Activity.Responses;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Common;
using MedApp.Contracts.Patients.Requests;
using MedApp.IntegrationTests.Common;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Privacy;

/// <summary>
/// The activity log through the API, with real SQL: what reading and changing patients, and signing in, records, and
/// that only admins can read it.
/// </summary>
[TestFixture]
public sealed class ActivityLogTests : IntegrationTest
{
    private HttpClient _admin = null!;

    [SetUp]
    public async Task SignInAsync() => _admin = await SignInAsAdminAsync();

    private async Task<List<ActivityEntryResponse>> ActivityAsync(string query)
    {
        var page = await (await _admin.GetAsync($"/api/activity/search?PageSize=100&{query}"))
            .ReadAsAsync<PagedResponse<ActivityEntryResponse>>();
        return page.Items.ToList();
    }

    [Test]
    public async Task ViewingAPatient_TwiceInAFewMinutes_RecordsOneViewByThatPerson()
    {
        var staff = await CreateStaffAsync(UserRole);
        var patient = await CreatePatientAsync(_admin);

        (await staff.Client.GetAsync($"/api/patients/{patient.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await staff.Client.GetAsync($"/api/patients/{patient.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var views = (await ActivityAsync($"PatientId={patient.Id}&Actions=PatientViewed"))
            .Where(e => e.UserId == staff.UserId)
            .ToList();
        var view = views.Should().ContainSingle().Subject;
        view.Username.Should().Be(staff.Username);
        view.PatientName.Should().Be($"{patient.FirstName} {patient.LastName}");
        // The in-memory test server gives requests no remote address, so IpAddress is null here; unit tests cover it.
    }

    [Test]
    public async Task SearchingPatients_RecordsTheFiltersAndResults_NeverTheTerms()
    {
        var staff = await CreateStaffAsync(UserRole);
        var patient = await CreatePatientAsync(_admin);

        var search = await staff.Client.GetAsync($"/api/patients/search?LastName={patient.LastName}");
        search.StatusCode.Should().Be(HttpStatusCode.OK);

        var mine = (await ActivityAsync($"UserId={staff.UserId}")).ToList();
        var searched = mine.Should().ContainSingle(e => e.Action == "PatientsSearched").Subject;
        searched.Details.Should().Contain("lastName").And.Contain("\"resultCount\":1").And.NotContain(patient.LastName);
        mine.Should().ContainSingle(e => e.Action == "PatientListed" && e.PatientId == patient.Id);
    }

    [Test]
    public async Task EditingAPatient_RecordsTheFieldsChanged_NotTheirValues()
    {
        var patient = await CreatePatientAsync(_admin);

        var response = await _admin.PatchAsJsonAsync($"/api/patients/{patient.Id}", new UpdatePatientRequest
        {
            FirstName = "Renamed",
            LastName = patient.LastName,
            DateOfBirth = DateOfBirth,
            Email = patient.Email,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var edit = (await ActivityAsync($"PatientId={patient.Id}&Actions=PatientUpdated")).Should().ContainSingle().Subject;
        edit.Details.Should().Be("{\"fields\":[\"firstName\"]}");
        (await ActivityAsync($"PatientId={patient.Id}&Actions=PatientCreated")).Should().ContainSingle();
    }

    [Test]
    public async Task SigningIn_RecordsSignInsAndRefusedOnes_WithTheUsernameTried()
    {
        var refused = await NewClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(AdminUsername, WrongPassword));
        refused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var entries = await ActivityAsync("Actions=SignInFailed&Actions=SignedIn");

        var failed = entries.Should().ContainSingle(e => e.Action == "SignInFailed").Subject;
        failed.Username.Should().Be(AdminUsername);
        failed.Details.Should().Contain("InvalidCredentials");
        entries.Should().Contain(e => e.Action == "SignedIn" && e.Username == AdminUsername);
    }

    [Test]
    public async Task SearchActivity_Staff_Is403()
    {
        var staff = await CreateStaffAsync(UserRole);

        (await staff.Client.GetAsync("/api/activity/search")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
