using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using MedApp.API.Common.Authentication;
using MedApp.Contracts.Authentication.Requests;
using MedApp.IntegrationTests.Common;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Auth;

/// <summary>
/// Who can call what, through the real authentication handler and policies: staff (Admin or User) versus admin-only
/// endpoints, and SelfUserOnly on changing a password. Authorisation runs before the handlers, so the IDs in the URLs
/// needn't exist.
/// </summary>
[TestFixture]
public sealed class AuthorisationTests : IntegrationTest
{
    private static readonly Guid AnyId = Guid.Parse("0f0e0d0c-0b0a-0908-0706-050403020100");

    private static IEnumerable<TestCaseData> AdminOnlyEndpoints()
    {
        var procedure = $"/api/procedures/patient-procedures/{AnyId}";
        var stages = "/api/procedures/patient-procedure-stage";
        yield return Case("GET", "/api/auth/users/search");
        yield return Case("GET", "/api/auth/users");
        yield return Case("GET", "/api/auth/users/someone");
        yield return Case("POST", "/api/auth/users");
        yield return Case("PUT", "/api/auth/users/someone/roles");
        yield return Case("PUT", "/api/auth/users/someone/reset-password");
        yield return Case("POST", "/api/auth/users/someone/sign-out-everywhere");
        yield return Case("DELETE", "/api/auth/users/someone");
        yield return Case("GET", "/api/auth/roles");
        yield return Case("POST", $"{stages}/definitions");
        yield return Case("PATCH", $"{stages}/definitions/{AnyId}");
        yield return Case("POST", $"{stages}/definitions/{AnyId}/archive");
        yield return Case("DELETE", $"{stages}/definitions/{AnyId}");
        yield return Case("POST", $"{stages}/templates");
        yield return Case("POST", $"{stages}/templates/{AnyId}/archive");
        yield return Case("DELETE", $"{stages}/templates/{AnyId}");
        yield return Case("POST", $"{procedure}/assignments/{AnyId}");
        yield return Case("DELETE", $"{procedure}/assignments/{AnyId}");
        yield return Case("POST", $"{procedure}/stages/{AnyId}/unlock");
        yield return Case("DELETE", procedure);
        yield return Case("GET", "/api/procedures/audit/search");
        yield return Case("GET", "/api/monitoring/rules");
        yield return Case("POST", "/api/monitoring/rules");
        yield return Case("PUT", $"/api/monitoring/rules/{AnyId}");
        yield return Case("PUT", $"/api/monitoring/rules/{AnyId}/enabled");
        yield return Case("DELETE", $"/api/monitoring/rules/{AnyId}");
    }

    private static IEnumerable<TestCaseData> StaffEndpoints()
    {
        yield return Case("GET", "/api/patients/search");
        yield return Case("GET", "/api/procedures/patient-procedure-stage/definitions");
        yield return Case("GET", "/api/procedures/patient-procedure-stage/templates");
        yield return Case("GET", "/api/procedures/patient-procedures/search");
        yield return Case("GET", "/api/notifications/preferences");
        yield return Case("GET", "/api/notifications");
        yield return Case("GET", "/api/auth/me/settings");
    }

    [TestCaseSource(nameof(AdminOnlyEndpoints))]
    public async Task AdminOnlyEndpoint_UserRole_Returns403(string method, string url)
    {
        var staff = await CreateStaffAsync(UserRole);

        var response = await SendAsync(staff.Client, method, url);

        await response.ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
    }

    [TestCaseSource(nameof(AdminOnlyEndpoints))]
    public async Task AdminOnlyEndpoint_SignedOut_Returns401(string method, string url)
    {
        await (await SendAsync(NewClient(), method, url)).ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
    }

    [TestCaseSource(nameof(StaffEndpoints))]
    public async Task StaffEndpoint_UserRole_Returns200(string method, string url)
    {
        var staff = await CreateStaffAsync(UserRole);

        await (await SendAsync(staff.Client, method, url)).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [TestCaseSource(nameof(StaffEndpoints))]
    public async Task StaffEndpoint_AdminRoleOnly_Returns200(string method, string url)
    {
        var admin = await CreateStaffAsync(AdminRole);

        await (await SendAsync(admin.Client, method, url)).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [TestCaseSource(nameof(StaffEndpoints))]
    public async Task StaffEndpoint_RolesRemoved_Returns403FromTheStaffPolicy(string method, string url)
    {
        // Chose their own password, then lost their roles (a leaver, say): nothing holds them but the Staff policy.
        // (An account that never had a role can't change its generated password, so the forced password change would
        // answer first: PasswordChangeRequiredTests covers that.)
        var staff = await CreateStaffAsync(UserRole);
        var admin = await SignInAsAdminAsync();
        await (await admin.PutAsJsonAsync($"/api/auth/users/{staff.Username}/roles", new SetUserRolesRequest([])))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        var response = await SendAsync(staff.Client, method, url);

        await response.ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().NotContain(PasswordChangeRequired.Code);
    }

    [TestCaseSource(nameof(StaffEndpoints))]
    public async Task StaffEndpoint_SignedOut_Returns401(string method, string url)
    {
        await (await SendAsync(NewClient(), method, url)).ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task UpdatePassword_OwnAccount_ChangesIt()
    {
        var staff = await CreateStaffAsync(UserRole);

        var response = await staff.Client.PutAsJsonAsync(
            $"/api/auth/users/{staff.Username}/update-password",
            new UpdateUserPasswordRequest(staff.Password, AnotherChosenPassword));

        await response.ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        await SignInAsync(staff.Username, AnotherChosenPassword);
        await (await NewClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(staff.Username, staff.Password)))
            .ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task UpdatePassword_SomeoneElsesAccount_Returns403AndKeepsTheirPassword()
    {
        var staff = await CreateStaffAsync(UserRole);
        var other = await CreateStaffAsync(UserRole);

        var response = await staff.Client.PutAsJsonAsync(
            $"/api/auth/users/{other.Username}/update-password",
            new UpdateUserPasswordRequest(other.Password, AnotherChosenPassword));

        await response.ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await SignInAsync(other.Username, other.Password);
    }

    [Test]
    public async Task UpdatePassword_AdminForSomeoneElse_Returns403()
    {
        var admin = await SignInAsAdminAsync();
        var staff = await CreateStaffAsync(UserRole);

        var response = await admin.PutAsJsonAsync(
            $"/api/auth/users/{staff.Username}/update-password",
            new UpdateUserPasswordRequest(staff.Password, AnotherChosenPassword));

        await response.ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task UpdatePassword_WrongOldPassword_Returns400()
    {
        var staff = await CreateStaffAsync(UserRole);

        var response = await staff.Client.PutAsJsonAsync(
            $"/api/auth/users/{staff.Username}/update-password",
            new UpdateUserPasswordRequest(WrongPassword, AnotherChosenPassword));

        await response.ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
    }

    private static TestCaseData Case(string method, string url) =>
        new TestCaseData(method, url).SetArgDisplayNames(method, url);

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT" or "PATCH" or "DELETE")
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        return client.SendAsync(request);
    }
}
