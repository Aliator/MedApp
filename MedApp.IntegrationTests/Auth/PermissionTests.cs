using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MedApp.API.Common.Authentication;
using MedApp.Application.Calendar.StaffCalendars.Queries.GetCalendarStaff;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.PatientPathways.Commands.AssignPatientPathway;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using Microsoft.AspNetCore.Mvc;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Authentication.Responses;
using MedApp.IntegrationTests.Common;
using MedApp.Infrastructure.Common.Identity;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Auth;

/// <summary>
/// Permissions through the real session handler and policies: for each permission, a starting role that has it gets
/// past authorisation and one that hasn't is refused with a 403 naming it; a permission taken from a role is refused on
/// the next request; an Administrator-only permission or an unknown name on an editable role grants nothing; and
/// <c>whoami</c> lists what the roles allow. Authorisation runs before the handlers, so the IDs needn't exist.
/// </summary>
[TestFixture]
public sealed class PermissionTests : IntegrationTest
{
    private static readonly Guid AnyId = Guid.Parse("0f0e0d0c-0b0a-0908-0706-050403020100");

    /// <summary>A role an admin made with the old role endpoints: no permissions, but UseApp, as every role.</summary>
    private const string MadeRole = "Visitor";

    /// <summary>
    /// For each permission with an endpoint: one it guards, a role that has the permission and one that hasn't.
    /// </summary>
    private static IEnumerable<TestCaseData> Guarded()
    {
        var pathway = $"/api/pathways/patient-pathways/{AnyId}";
        var stages = "/api/pathways/patient-pathway-stage";
        yield return Case(Permission.ViewPatients, "GET", "/api/patients/search", "Lab", MadeRole);
        yield return Case(Permission.EditPatients, "POST", "/api/patients", "Nurse", "Lab");
        yield return Case(Permission.DeletePatients, "DELETE", $"/api/patients/{AnyId}", "Booking", "Nurse");
        yield return Case(Permission.RegisterReferrals, "POST", "/api/referrals", "Booking", "Lab");
        yield return Case(Permission.TriageReferrals, "POST", $"/api/referrals/{AnyId}/reject", "Clinician", "Nurse");
        yield return Case(Permission.CreatePathways, "POST", "/api/pathways/patient-pathways", "Nurse", "Booking");
        yield return Case(Permission.WorkPathways, "POST", $"{pathway}/stages/{AnyId}/start", "Lab", "Booking");
        yield return Case(Permission.AssignPathways, "POST", $"{pathway}/assignments/{AnyId}", "Manager", "Clinician");
        yield return Case(Permission.OverrideStages, "POST", $"{pathway}/stages/{AnyId}/unlock", "Clinician", "Nurse");
        yield return Case(Permission.DeletePathways, "DELETE", pathway, AdminRole, "Clinician");
        yield return Case(Permission.BookAppointments, "POST", "/api/appointments", "Booking", "Lab");
        yield return Case(Permission.ArrangeMeetings, "GET", "/api/calendar/team", "Manager", "Nurse");
        yield return Case(Permission.AttachFiles, "POST", $"/api/attachments?patientId={AnyId}", "Lab", "Manager");
        yield return Case(Permission.ViewInsights, "GET", "/api/pathways/insights", "Manager", "Clinician");
        yield return Case(Permission.ManageMonitoring, "GET", "/api/monitoring/rules", "Manager", "Clinician");
        yield return Case(Permission.ManageSetup, "POST", $"{stages}/definitions", AdminRole, "Manager");
        yield return Case(Permission.ManageUsers, "GET", "/api/auth/users/search", AdminRole, "Manager");
        yield return Case(Permission.ManageRoles, "POST", "/api/auth/roles", AdminRole, "Manager");
        yield return Case(Permission.ManageSecuritySettings, "GET", "/api/auth/security-settings", AdminRole, "Manager");
        yield return Case(Permission.ViewAudit, "GET", "/api/activity/search", AdminRole, "Manager");
        yield return Case(Permission.ExportPatientRecords, "GET", $"/api/patients/{AnyId}/export", AdminRole, "Clinician");
        yield return Case(Permission.ManagePrivacy, "GET", "/api/patients/retention-review", AdminRole, "Manager");
        yield return Case(Permission.CorrectRecords, "GET", $"/api/pathways/note-deletion-requests/{AnyId}", AdminRole, "Clinician");
    }

    [TestCaseSource(nameof(Guarded))]
    public async Task Endpoint_RoleWithThePermission_GetsPastAuthorisation(Permission permission, string method, string url, string allowed, string refused)
    {
        var staff = await CreateStaffAsync(allowed);

        var response = await SendAsync(staff.Client, method, url);

        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden, $"{allowed} has {permission}");
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [TestCaseSource(nameof(Guarded))]
    public async Task Endpoint_RoleWithoutThePermission_Is403NamingIt(Permission permission, string method, string url, string allowed, string refused)
    {
        if (refused == MadeRole)
            await MakeRoleAsync(MadeRole);
        var staff = await CreateStaffAsync(refused);

        var response = await SendAsync(staff.Client, method, url);

        await ShouldBeMissingAsync(response, permission);
    }

    [Test]
    public async Task UseApp_EveryRoleHasIt_AnAccountWithNoRoleIsRefused()
    {
        await MakeRoleAsync(MadeRole);
        var withMadeRole = await CreateStaffAsync(MadeRole);
        var noRole = await CreateStaffAsync("Lab");
        await (await (await SignInAsAdminAsync()).PutAsJsonAsync($"/api/auth/users/{noRole.Username}/roles", new SetUserRolesRequest([])))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await (await withMadeRole.Client.GetAsync("/api/notifications")).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await ShouldBeMissingAsync(await noRole.Client.GetAsync("/api/notifications"), Permission.UseApp);
    }

    [Test]
    public async Task ManageAnyCalendar_LetsBookingChangeSomeoneElsesDay_ButNotANurse()
    {
        var colleague = await CreateStaffAsync("Clinician");
        var booking = await CreateStaffAsync("Booking");
        var nurse = await CreateStaffAsync("Nurse");
        var url = $"/api/calendar/staff/{colleague.UserId}/days/{NextWeekday():yyyy-MM-dd}/day-template";

        // Booking gets as far as finding the day has no template of its own; the nurse is stopped by the handler.
        await (await booking.Client.DeleteAsync(url)).ShouldHaveStatusAsync(HttpStatusCode.NotFound);
        await (await nurse.Client.DeleteAsync(url)).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task PermissionTakenFromARole_IsRefusedOnTheNextRequest()
    {
        var nurse = await CreateStaffAsync("Nurse");
        await (await nurse.Client.GetAsync("/api/patients/search")).ShouldHaveStatusAsync(HttpStatusCode.OK);

        await ExecuteSqlAsync($"DELETE FROM RolePermissions WHERE RoleId = {StandardRoles.Nurse.Id} AND Permission = 'ViewPatients'");

        await ShouldBeMissingAsync(await nurse.Client.GetAsync("/api/patients/search"), Permission.ViewPatients);
    }

    [Test]
    public async Task AdministratorOnlyOrUnknownPermission_OnAnEditableRole_GrantsNothing()
    {
        var nurse = await CreateStaffAsync("Nurse");

        await ExecuteSqlAsync($"INSERT INTO RolePermissions (RoleId, Permission) VALUES ({StandardRoles.Nurse.Id}, 'ManageUsers'), ({StandardRoles.Nurse.Id}, 'SeeEverything')");

        await ShouldBeMissingAsync(await nurse.Client.GetAsync("/api/auth/users/search"), Permission.ManageUsers);
        var me = await (await nurse.Client.GetAsync("/api/auth/whoami")).ReadAsAsync<WhoAmIResponse>();
        me.Permissions.Should().NotContain(["ManageUsers", "SeeEverything"]);
    }

    [Test]
    public async Task WhoAmI_ListsWhatTheRolesAllow_Together_Sorted()
    {
        var staff = await CreateStaffAsync("Lab", "Manager");

        var me = await (await staff.Client.GetAsync("/api/auth/whoami")).ReadAsAsync<WhoAmIResponse>();

        me.Roles.Should().BeEquivalentTo(["Lab", "Manager"]);
        me.Permissions.Should().Equal(
            StandardRoles.Lab.Permissions.Union(StandardRoles.Manager.Permissions)
                .Select(p => p.ToString())
                .Order(StringComparer.Ordinal));
    }

    [Test]
    public async Task WhoAmI_Admin_HasTheFixedSet()
    {
        var admin = await SignInAsAdminAsync();

        var me = await (await admin.GetAsync("/api/auth/whoami")).ReadAsAsync<WhoAmIResponse>();

        me.Permissions.Should().BeEquivalentTo(Permissions.Admin.Select(p => p.ToString()));
    }

    [Test]
    public async Task ArchivedRole_KeepsGrantingItsHolders_ButCantBeGivenToAnyoneNew()
    {
        var holder = await CreateStaffAsync("Lab");
        var newcomer = await CreateStaffAsync("Nurse");
        await ExecuteSqlAsync($"UPDATE AspNetRoles SET IsArchived = 1 WHERE Id = {StandardRoles.Lab.Id}");

        await (await holder.Client.GetAsync("/api/patients/search")).ShouldHaveStatusAsync(HttpStatusCode.OK);
        var response = await (await SignInAsAdminAsync())
            .PutAsJsonAsync($"/api/auth/users/{newcomer.Username}/roles", new SetUserRolesRequest(["Nurse", "Lab"]));
        await response.ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain(IdentityErrors.RoleArchivedCode);
    }

    [Test]
    public async Task AssigningAPathway_OnlyToSomeoneWhoseRolesAllowWorkingOnPathways()
    {
        var admin = await SignInAsAdminAsync();
        var booking = await CreateStaffAsync("Booking");
        var nurse = await CreateStaffAsync("Nurse");
        var pathway = await CreatePatientWithPathwayAsync(admin);

        var refused = await admin.PostAsync($"{PathwayUrl(pathway.Id)}/assignments/{booking.UserId}", null);
        var problem = await refused.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainKey("UserId")
            .WhoseValue.Should().Equal(AssignPatientPathwayHandler.CantWorkOnPathways(booking.Username));

        var assigned = await (await admin.PostAsync($"{PathwayUrl(pathway.Id)}/assignments/{nurse.UserId}", null))
            .ReadAsAsync<PatientPathwayResponse>();
        assigned.Assignments.Should().ContainSingle(a => a.UserId == nurse.UserId);
    }

    [Test]
    public async Task PathwayResponse_FlagsFollowTheViewersPermissions()
    {
        var admin = await SignInAsAdminAsync();
        var manager = await CreateStaffAsync("Manager");
        var nurse = await CreateStaffAsync("Nurse");
        var pathway = await CreatePatientWithPathwayAsync(admin);

        var asManager = await (await manager.Client.GetAsync(PathwayUrl(pathway.Id))).ReadAsAsync<PatientPathwayResponse>();
        var asNurse = await (await nurse.Client.GetAsync(PathwayUrl(pathway.Id))).ReadAsAsync<PatientPathwayResponse>();

        asManager.Should().BeEquivalentTo(new { CanWorkOnStages = false, CanAddNotes = false, CanEditDetails = false, CanChangeAssignments = true });
        asManager.AllowedStatusChanges.Should().BeEmpty();
        asNurse.Should().BeEquivalentTo(new { CanWorkOnStages = true, CanAddNotes = true, CanEditDetails = true, CanChangeAssignments = false });
        asNurse.AllowedStatusChanges.Should().NotBeEmpty();
    }

    [Test]
    public async Task StaffList_WithAPermission_ListsOnlyItsHolders_AndRefusesAnUnknownName()
    {
        var booking = await CreateStaffAsync("Booking");
        var nurse = await CreateStaffAsync("Nurse");
        var manager = await CreateStaffAsync("Manager");

        // A Manager, who can't manage users, reads who can be assigned.
        var holders = await (await manager.Client.GetAsync("/api/calendar/staff?permission=WorkPathways"))
            .ReadAsAsync<List<StaffMemberResponse>>();
        holders.Select(s => s.UserId).Should().Contain([nurse.UserId, await GetUserIdAsync(await SignInAsAdminAsync(), AdminUsername)])
            .And.NotContain([booking.UserId, manager.UserId]);

        var everyone = await (await manager.Client.GetAsync("/api/calendar/staff")).ReadAsAsync<List<StaffMemberResponse>>();
        everyone.Select(s => s.UserId).Should().Contain([booking.UserId, nurse.UserId, manager.UserId]);

        var problem = await (await manager.Client.GetAsync("/api/calendar/staff?permission=Nope"))
            .ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainKey("Permission")
            .WhoseValue.Should().Equal(GetCalendarStaffValidator.UnknownPermission("Nope"));
    }

    private static async Task ShouldBeMissingAsync(HttpResponseMessage response, Permission permission)
    {
        await response.ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("code").GetString().Should().Be(ApiAuthorizationResultHandler.PermissionMissingCode);
        problem.RootElement.GetProperty("permission").GetString().Should().Be(permission.ToString());
        problem.RootElement.GetProperty("detail").GetString().Should().Contain(Permissions.Describe(permission).Description);
    }

    private static async Task MakeRoleAsync(string name) =>
        await (await (await SignInAsAdminAsync()).PostAsJsonAsync("/api/auth/roles", new CreateRoleRequest(name)))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);

    private static DateOnly NextWeekday()
    {
        var day = DateOnly.FromDateTime(Now).AddDays(7);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            day = day.AddDays(1);
        return day;
    }

    private static TestCaseData Case(Permission permission, string method, string url, string allowed, string refused) =>
        new TestCaseData(permission, method, url, allowed, refused).SetArgDisplayNames(permission.ToString(), allowed, refused);

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (url.StartsWith("/api/attachments", StringComparison.Ordinal))
        {
            request.Content = new ByteArrayContent("synthetic"u8.ToArray());
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            request.Headers.Add("X-File-Name", "synthetic.txt");
        }
        else if (method is "POST" or "PUT" or "PATCH" or "DELETE")
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        return client.SendAsync(request);
    }
}
