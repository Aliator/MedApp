using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Domain.Activity;
using MedApp.IntegrationTests.Common;
using MedApp.Infrastructure.Common.Identity;
using MedApp.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Auth;

/// <summary>
/// Nobody locks everyone out (decision 57): the last active admin keeps Admin and their account, the built-in Admin
/// role can't be deleted, and <c>grant-admin</c> recovers when every admin is locked out anyway.
/// </summary>
[TestFixture]
public sealed class AdminSafetyTests : IntegrationTest
{
    [Test]
    public async Task DeleteUser_TheLastActiveAdminThemselves_Is409_AndTheyStay()
    {
        var admin = await SignInAsAdminAsync();

        var response = await admin.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/auth/users/{AdminUsername}")
        {
            Content = JsonContent.Create(new DeleteUserRequest(AdminPassword)),
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain(IdentityErrors.LastAdminCode);
        await SignInAsAdminAsync();
    }

    [Test]
    public async Task DeleteUser_AnotherAdminRemains_Deletes()
    {
        var second = await CreateStaffAsync(AdminRole);

        var response = await second.Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/auth/users/{AdminUsername}")
        {
            Content = JsonContent.Create(new DeleteUserRequest(second.Password)),
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task SetUserRoles_TakingAdminFromTheLastActiveAdmin_Is409()
    {
        // The first admin is locked out (wrong passwords elsewhere) but still has a session; the second is the last
        // active admin, so the first can't take Admin from them.
        var first = await SignInAsAdminAsync();
        var second = await CreateStaffAsync(AdminRole);
        await LockOutAsync(AdminUsername);

        var response = await first.PutAsJsonAsync($"/api/auth/users/{second.Username}/roles", new SetUserRolesRequest(["Clinician"]));

        await response.ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        (await RolesOfAsync(second.Username)).Should().Contain(AppRoles.Admin);
    }

    [Test]
    public async Task SetUserRoles_AnotherActiveAdminRemains_TakesAdmin()
    {
        var first = await SignInAsAdminAsync();
        var second = await CreateStaffAsync(AdminRole);

        var response = await first.PutAsJsonAsync($"/api/auth/users/{second.Username}/roles", new SetUserRolesRequest(["Clinician"]));

        await response.ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        (await RolesOfAsync(second.Username)).Should().Equal("Clinician");
    }

    [Test]
    public async Task SetUserRoles_OwnRoles_Is400()
    {
        var admin = await SignInAsAdminAsync();

        var response = await admin.PutAsJsonAsync($"/api/auth/users/{AdminUsername}/roles", new SetUserRolesRequest(["Clinician"]));

        await response.ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
        (await RolesOfAsync(AdminUsername)).Should().Contain(AppRoles.Admin);
    }

    [Test]
    public async Task RevokeRole_OwnAdminOrTheLastActiveAdmins_IsRefused()
    {
        var first = await SignInAsAdminAsync();
        var second = await CreateStaffAsync(AdminRole);

        await (await first.PostAsJsonAsync("/api/auth/roles/revoke", new RevokeRoleRequest(AdminUsername, AppRoles.Admin)))
            .ShouldHaveStatusAsync(HttpStatusCode.BadRequest);

        await LockOutAsync(AdminUsername);
        await (await first.PostAsJsonAsync("/api/auth/roles/revoke", new RevokeRoleRequest(second.Username, AppRoles.Admin)))
            .ShouldHaveStatusAsync(HttpStatusCode.BadRequest);

        (await RolesOfAsync(AdminUsername)).Should().Contain(AppRoles.Admin);
        (await RolesOfAsync(second.Username)).Should().Contain(AppRoles.Admin);
    }

    [Test]
    public async Task RevokeRole_TwoAdminsRevokingEachOthersAdminAtOnce_LeaveOneAdmin()
    {
        // Two admins only: each takes Admin from the other at the same moment. The guard's lock lets one through, and
        // the other then finds the last active admin and is refused.
        var first = await SignInAsAdminAsync();
        var second = await CreateStaffAsync(AdminRole);

        var responses = await Task.WhenAll(
            first.PostAsJsonAsync("/api/auth/roles/revoke", new RevokeRoleRequest(second.Username, AppRoles.Admin)),
            second.Client.PostAsJsonAsync("/api/auth/roles/revoke", new RevokeRoleRequest(AdminUsername, AppRoles.Admin)));

        responses.Count(r => r.StatusCode == HttpStatusCode.NoContent).Should().Be(1);
        var admins = 0;
        foreach (var username in new[] { AdminUsername, second.Username })
            if ((await RolesOfAsync(username)).Contains(AppRoles.Admin))
                admins++;
        admins.Should().Be(1);
    }

    [Test]
    public async Task DeleteRole_Admin_Is409()
    {
        var admin = await SignInAsAdminAsync();

        var response = await admin.DeleteAsync($"/api/auth/roles/{AppRoles.Admin}");

        await response.ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        (await RolesOfAsync(AdminUsername)).Should().Contain(AppRoles.Admin);
    }

    [Test]
    public async Task GrantAdmin_GivesTheAccountAdmin_AndLogsItAsASecurityEvent()
    {
        var staff = await CreateStaffAsync("Nurse");

        var outcome = await RunInScopeAsync(services => services.GetRequiredService<AdminGrant>().RunAsync(staff.Username, CancellationToken.None));

        outcome.Should().Be(AdminGrantOutcome.Granted);
        (await RolesOfAsync(staff.Username)).Should().BeEquivalentTo([AppRoles.Admin, "Nurse"]);
        var entry = await RunInScopeAsync(services => services.GetRequiredService<MedAppDbContext>().ActivityEntries
            .SingleAsync(e => e.Action == ActivityAction.RolesChanged && e.TargetUsername == staff.Username && e.Details!.Contains("grant-admin")));
        entry.UserId.Should().BeNull("nobody is signed in for a command run by whoever deploys");
        var me = await (await staff.Client.GetAsync("/api/auth/whoami")).ReadAsAsync<WhoAmIResponse>();
        me.Permissions.Should().Contain(nameof(Permission.ManageUsers), "the next request reads the new role");
    }

    [Test]
    public async Task GrantAdmin_AlreadyAdminOrUnknown_ChangesNothing()
    {
        var already = await RunInScopeAsync(services => services.GetRequiredService<AdminGrant>().RunAsync(AdminUsername, CancellationToken.None));
        var unknown = await RunInScopeAsync(services => services.GetRequiredService<AdminGrant>().RunAsync("nobody-here", CancellationToken.None));

        already.Should().Be(AdminGrantOutcome.AlreadyAdmin);
        unknown.Should().Be(AdminGrantOutcome.UserNotFound);
    }

    private static Task LockOutAsync(string username) =>
        ExecuteSqlAsync($"UPDATE AspNetUsers SET LockoutEnd = {DateTimeOffset.UtcNow.AddDays(1)} WHERE UserName = {username}");

    private static Task<IList<string>> RolesOfAsync(string username) =>
        RunInScopeAsync(async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            return await users.GetRolesAsync((await users.FindByNameAsync(username))!);
        });
}
