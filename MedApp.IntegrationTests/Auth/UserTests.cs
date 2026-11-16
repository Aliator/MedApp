using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Contracts.Common;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Identity;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Auth;

/// <summary>Managing accounts through the admin API, against real Identity stores.</summary>
[TestFixture]
public sealed class UserTests : IntegrationTest
{
    private HttpClient _admin = null!;

    [SetUp]
    public async Task SignInAsync() => _admin = await SignInAsAdminAsync();

    [Test]
    public async Task CreateUser_NewUsername_ReturnsAGeneratedPasswordThatSignsIn()
    {
        var username = UniqueName("new");

        var response = await _admin.PostAsJsonAsync("/api/auth/users", new CreateUserRequest(username));

        var created = await response.ReadAsAsync<CreateUserResponse>();
        created.Username.Should().Be(username);
        created.Password.Should().HaveLength(16)
            .And.MatchRegex("[A-Z]").And.MatchRegex("[a-z]").And.MatchRegex("[0-9]");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        await SignInAsync(username, created.Password);
    }

    [Test]
    public async Task CreateUser_UsernameTaken_Returns400WithIdentityErrors()
    {
        var username = UniqueName("taken");
        await (await _admin.PostAsJsonAsync("/api/auth/users", new CreateUserRequest(username))).ShouldHaveStatusAsync(HttpStatusCode.OK);

        var response = await _admin.PostAsJsonAsync("/api/auth/users", new CreateUserRequest(username));

        var errors = await response.ReadAsAsync<List<IdentityError>>(HttpStatusCode.BadRequest);
        errors.Should().Contain(e => e.Code == "DuplicateUserName");
    }

    [Test]
    public async Task ResetPassword_ExistingUser_ReplacesThePasswordWithANewGeneratedOne()
    {
        var staff = await CreateStaffAsync(UserRole);

        var response = await _admin.PutAsync($"/api/auth/users/{staff.Username}/reset-password", null);

        // A bare string comes back as text/plain, not JSON.
        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var generated = await response.Content.ReadAsStringAsync();
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        generated.Should().NotBe(staff.Password).And.HaveLength(16);
        await SignInAsync(staff.Username, generated);
        await (await NewClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(staff.Username, staff.Password)))
            .ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ResetPassword_UnknownUser_Returns400()
    {
        await (await _admin.PutAsync($"/api/auth/users/{UniqueName("nobody")}/reset-password", null))
            .ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task SetUserRoles_DifferentRoles_ReplacesThemInOneStep()
    {
        var staff = await CreateStaffAsync(UserRole);

        var response = await _admin.PutAsJsonAsync($"/api/auth/users/{staff.Username}/roles", new SetUserRolesRequest([AdminRole]));

        await response.ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        (await RolesOfAsync(staff.Username)).Should().BeEquivalentTo([AdminRole]);
        // The session picks the new roles up on its next request.
        var me = await (await staff.Client.GetAsync("/api/auth/whoami")).ReadAsAsync<WhoAmIResponse>();
        me.Roles.Should().BeEquivalentTo([AdminRole]);
        await (await staff.Client.GetAsync("/api/auth/users/search")).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task SetUserRoles_BothRolesThenNone_AddsAndRemovesThem()
    {
        var staff = await CreateStaffAsync(UserRole);

        await (await _admin.PutAsJsonAsync($"/api/auth/users/{staff.Username}/roles", new SetUserRolesRequest([AdminRole, UserRole])))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        (await RolesOfAsync(staff.Username)).Should().BeEquivalentTo([AdminRole, UserRole]);

        await (await _admin.PutAsJsonAsync($"/api/auth/users/{staff.Username}/roles", new SetUserRolesRequest([])))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        (await RolesOfAsync(staff.Username)).Should().BeEmpty();
        await (await staff.Client.GetAsync("/api/patients/search")).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task SetUserRoles_UnknownRole_Returns400AndKeepsTheRolesTheyHad()
    {
        var staff = await CreateStaffAsync(UserRole);

        var response = await _admin.PutAsJsonAsync(
            $"/api/auth/users/{staff.Username}/roles", new SetUserRolesRequest([AdminRole, "NoSuchRole"]));

        await response.ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
        (await RolesOfAsync(staff.Username)).Should().BeEquivalentTo([UserRole]);
    }

    [Test]
    public async Task SetUserRoles_OwnAccount_Returns400()
    {
        var response = await _admin.PutAsJsonAsync($"/api/auth/users/{AdminUsername}/roles", new SetUserRolesRequest([UserRole]));

        await response.ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
        (await RolesOfAsync(AdminUsername)).Should().BeEquivalentTo([AdminRole, UserRole]);
    }

    [Test]
    public async Task SetUserRoles_UnknownUser_Returns404()
    {
        await (await _admin.PutAsJsonAsync($"/api/auth/users/{UniqueName("nobody")}/roles", new SetUserRolesRequest([UserRole])))
            .ShouldHaveStatusAsync(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task SearchUsers_ByRole_ReturnsOnlyThatRolesUsersSortedByName()
    {
        var first = await CreateStaffAsync(UserRole);
        var second = await CreateStaffAsync(UserRole);
        await CreateStaffAsync(AdminRole);

        var page = await (await _admin.GetAsync("/api/auth/users/search?role=User&sort=username"))
            .ReadAsAsync<PagedResponse<UserResponse>>();

        // The first admin has User too.
        page.Items.Select(u => u.Username).Should()
            .BeEquivalentTo([AdminUsername, first.Username, second.Username])
            .And.BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
        page.TotalCount.Should().Be(3);
    }

    [Test]
    public async Task DeleteUser_RightAdminPassword_DeletesTheAccountAndEndsItsSessions()
    {
        var staff = await CreateStaffAsync(UserRole);

        var response = await _admin.DeleteAsJsonAsync($"/api/auth/users/{staff.Username}", new DeleteUserRequest(AdminPassword));

        await response.ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        await (await _admin.GetAsync($"/api/auth/users/{staff.Username}")).ShouldHaveStatusAsync(HttpStatusCode.NotFound);
        await (await staff.Client.GetAsync("/api/patients/search")).ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task DeleteUser_WrongAdminPassword_Returns400AndKeepsTheAccount()
    {
        var staff = await CreateStaffAsync(UserRole);

        var response = await _admin.DeleteAsJsonAsync($"/api/auth/users/{staff.Username}", new DeleteUserRequest(WrongPassword));

        await response.ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
        await (await _admin.GetAsync($"/api/auth/users/{staff.Username}")).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task DeleteUser_UnknownUser_Returns404()
    {
        await (await _admin.DeleteAsJsonAsync($"/api/auth/users/{UniqueName("nobody")}", new DeleteUserRequest(AdminPassword)))
            .ShouldHaveStatusAsync(HttpStatusCode.NotFound);
    }

    private async Task<IReadOnlyList<string>> RolesOfAsync(string username) =>
        await (await _admin.GetAsync($"/api/auth/users/{username}/roles")).ReadAsAsync<List<string>>();
}
