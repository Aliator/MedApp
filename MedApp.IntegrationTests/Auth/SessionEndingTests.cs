using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Infrastructure.Common.Identity;
using MedApp.IntegrationTests.Common;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Auth;

/// <summary>
/// Which sessions end when an account changes, through the real API and database: signing a user out everywhere,
/// changing your own password, and the common-password list on a new password.
/// </summary>
[TestFixture]
public sealed class SessionEndingTests : IntegrationTest
{
    private static async Task<bool> IsSignedInAsync(HttpClient client) =>
        (await (await client.GetAsync("/api/auth/whoami")).ReadAsAsync<WhoAmIResponse>()).IsAuthenticated;

    [Test]
    public async Task SignOutEverywhere_EndsEverySessionTheUserHas_AndNoOneElses()
    {
        var staff = await CreateStaffAsync(UserRole);
        var secondSession = await SignInAsync(staff.Username, staff.Password);
        var other = await CreateStaffAsync(UserRole);
        var admin = await SignInAsAdminAsync();

        await (await admin.PostAsync($"/api/auth/users/{staff.Username}/sign-out-everywhere", null))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        (await IsSignedInAsync(staff.Client)).Should().BeFalse();
        (await IsSignedInAsync(secondSession)).Should().BeFalse();
        await (await staff.Client.GetAsync("/api/patients/search")).ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
        (await IsSignedInAsync(other.Client)).Should().BeTrue();
        (await IsSignedInAsync(admin)).Should().BeTrue();
        // They can sign in again: their account and password are as they were.
        (await IsSignedInAsync(await SignInAsync(staff.Username, staff.Password))).Should().BeTrue();
    }

    [Test]
    public async Task SignOutEverywhere_UnknownUser_Returns404()
    {
        var admin = await SignInAsAdminAsync();

        await (await admin.PostAsync($"/api/auth/users/{UniqueName("nobody")}/sign-out-everywhere", null))
            .ShouldHaveStatusAsync(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task UpdatePassword_EndsTheUsersOtherSessions_AndKeepsThisOne()
    {
        var staff = await CreateStaffAsync(UserRole);
        var elsewhere = await SignInAsync(staff.Username, staff.Password);

        await (await staff.Client.PutAsJsonAsync(
                $"/api/auth/users/{staff.Username}/update-password",
                new UpdateUserPasswordRequest(staff.Password, AnotherChosenPassword)))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        (await IsSignedInAsync(staff.Client)).Should().BeTrue();
        (await IsSignedInAsync(elsewhere)).Should().BeFalse();
    }

    [Test]
    public async Task UpdatePassword_CommonPassword_Returns400AndKeepsTheOldOne()
    {
        var staff = await CreateStaffAsync(UserRole);

        var response = await staff.Client.PutAsJsonAsync(
            $"/api/auth/users/{staff.Username}/update-password",
            new UpdateUserPasswordRequest(staff.Password, "Password1"));

        await response.ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain(CommonPasswordValidator.ErrorCode);
        (await IsSignedInAsync(await SignInAsync(staff.Username, staff.Password))).Should().BeTrue();
    }
}
