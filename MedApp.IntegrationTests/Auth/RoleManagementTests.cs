using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Authentication.Roles.Common;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Activity.Responses;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Common;
using MedApp.Contracts.Roles.Requests;
using MedApp.Contracts.Roles.Responses;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Auth;

/// <summary>
/// The Roles and Permissions page's API (<c>api/roles</c>, <c>api/permissions</c>) against SQL Server: making,
/// changing, archiving and deleting roles; the built-in Admin role and the Administrator-only permissions refused; a
/// role someone holds kept; a removed permission refused on the holder's next request; two admins saving one role at
/// once; and each change in the activity log and emailed to the other admins.
/// </summary>
[TestFixture]
public sealed class RoleManagementTests : IntegrationTest
{
    private const string Roles = "/api/roles";

    private HttpClient _admin = null!;

    [SetUp]
    public async Task SignIn() => _admin = await SignInAsAdminAsync();

    private async Task<RoleResponse> CreateRoleAsync(string name, params string[] permissions) =>
        await (await _admin.PostAsJsonAsync(Roles, new CreateRoleRequest { Name = name, Description = "Synthetic role.", Permissions = permissions }))
            .ReadAsAsync<RoleResponse>(HttpStatusCode.Created);

    private static UpdateRoleRequest Update(RoleResponse role, params string[] permissions) =>
        new() { Name = role.Name, Description = role.Description, Permissions = permissions, ExpectedVersion = role.Version };

    private async Task<IReadOnlyList<RoleResponse>> GetRolesAsync() =>
        await (await _admin.GetAsync(Roles)).ReadAsAsync<List<RoleResponse>>();

    private async Task<RoleResponse> BuiltInAdminAsync() => (await GetRolesAsync()).Single(r => r.IsBuiltIn);

    private async Task<PagedResponse<ActivityEntryResponse>> ActivityAsync(string action) =>
        await (await _admin.GetAsync($"/api/activity/search?PageSize=100&Actions={action}")).ReadAsAsync<PagedResponse<ActivityEntryResponse>>();

    [Test]
    public async Task ARole_IsMadeChangedArchivedRestoredAndDeleted_EachStepLogged()
    {
        var made = await CreateRoleAsync("Synthetic visiting team", "ViewPatients");
        made.Permissions.Should().Equal("ViewPatients");
        made.HolderCount.Should().Be(0);

        var listed = await GetRolesAsync();
        listed[0].Name.Should().Be(AppRoles.Admin);
        listed.Should().Contain(r => r.Id == made.Id && r.Description == "Synthetic role." && !r.IsArchived);

        var changed = await (await _admin.PutAsJsonAsync($"{Roles}/{made.Id}", Update(made, "ViewPatients", "WorkPathways")))
            .ReadAsAsync<RoleResponse>();
        changed.Permissions.Should().Equal("ViewPatients", "WorkPathways");
        changed.Version.Should().NotBe(made.Version);

        var archived = await (await _admin.PostAsJsonAsync($"{Roles}/{made.Id}/archive", new RoleVersionRequest { ExpectedVersion = changed.Version }))
            .ReadAsAsync<RoleResponse>();
        archived.IsArchived.Should().BeTrue();
        var restored = await (await _admin.PostAsJsonAsync($"{Roles}/{made.Id}/restore", new RoleVersionRequest { ExpectedVersion = archived.Version }))
            .ReadAsAsync<RoleResponse>();
        restored.IsArchived.Should().BeFalse();

        await (await _admin.DeleteAsync($"{Roles}/{made.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        await (await _admin.GetAsync($"{Roles}/{made.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NotFound);

        (await ActivityAsync("RoleCreated")).Items.Should().ContainSingle(e => e.Username == AdminUsername && e.Details.Contains("ViewPatients"));
        var changes = (await ActivityAsync("RoleChanged")).Items;
        changes.Should().HaveCount(3).And.OnlyContain(e => e.Username == AdminUsername);
        changes.Should().Contain(e => e.Details.Contains("\"added\":[\"WorkPathways\"]"));
        changes.Should().Contain(e => e.Details.Contains("\"archived\":true"));
        (await ActivityAsync("RoleDeleted")).Items.Should().ContainSingle();
    }

    [Test]
    public async Task AnAdministratorOnlyPermission_IsRefusedOnAnyOtherRole_AndNothingIsSaved()
    {
        var created = await _admin.PostAsJsonAsync(Roles, new CreateRoleRequest { Name = "Synthetic auditors", Permissions = ["ViewAudit"] });
        var problem = await created.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest);
        problem.Errors.Values.SelectMany(v => v).Should().Contain(RoleRules.AdminOnlyMessage("ViewAudit"));
        (await GetRolesAsync()).Should().NotContain(r => r.Name == "Synthetic auditors");

        var role = await CreateRoleAsync("Synthetic helpers", "ViewPatients");
        await (await _admin.PutAsJsonAsync($"{Roles}/{role.Id}", Update(role, "ViewPatients", "ManageUsers")))
            .ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
        (await (await _admin.GetAsync($"{Roles}/{role.Id}")).ReadAsAsync<RoleResponse>()).Permissions.Should().Equal("ViewPatients");
    }

    [Test]
    public async Task TheBuiltInAdminRole_RefusesEveryChange()
    {
        var admin = await BuiltInAdminAsync();
        admin.CanEdit.Should().BeFalse();
        admin.Permissions.Should().Contain("ManageRoles").And.NotContain("TriageReferrals");

        await (await _admin.PutAsJsonAsync($"{Roles}/{admin.Id}", Update(admin, "ViewPatients"))).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await (await _admin.PostAsJsonAsync($"{Roles}/{admin.Id}/archive", new RoleVersionRequest { ExpectedVersion = admin.Version }))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await (await _admin.DeleteAsync($"{Roles}/{admin.Id}")).ShouldHaveStatusAsync(HttpStatusCode.Conflict);

        (await BuiltInAdminAsync()).Should().BeEquivalentTo(admin);
        await SignInAsAdminAsync();
    }

    [Test]
    public async Task ARoleSomeoneHolds_IsArchivedNotDeleted_AndTheyKeepItsPermissions()
    {
        var role = await CreateRoleAsync("Synthetic readers", "ViewPatients");
        var holder = await CreateStaffAsync(role.Name);

        var refused = await _admin.DeleteAsync($"{Roles}/{role.Id}");
        await refused.ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("Archive it instead");

        var detail = await (await _admin.GetAsync($"{Roles}/{role.Id}")).ReadAsAsync<RoleResponse>();
        detail.HolderCount.Should().Be(1);
        detail.CanDelete.Should().BeFalse();
        detail.Holders.Should().ContainSingle(h => h.Username == holder.Username && h.UserId == holder.UserId);

        await (await _admin.PostAsJsonAsync($"{Roles}/{role.Id}/archive", new RoleVersionRequest { ExpectedVersion = detail.Version }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await holder.Client.GetAsync("/api/patients/search")).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task APermissionRemovedFromARole_IsRefusedOnItsHoldersNextRequest()
    {
        var role = await CreateRoleAsync("Synthetic bookers", "ViewPatients", "BookAppointments");
        var holder = await CreateStaffAsync(role.Name);
        await (await holder.Client.GetAsync("/api/patients/search")).ShouldHaveStatusAsync(HttpStatusCode.OK);

        await (await _admin.PutAsJsonAsync($"{Roles}/{role.Id}", Update(role, "BookAppointments"))).ShouldHaveStatusAsync(HttpStatusCode.OK);

        await (await holder.Client.GetAsync("/api/patients/search")).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task TwoAdminsSavingOneRoleAtOnce_OneIsSaved_TheOtherIsTold()
    {
        var second = await CreateStaffAsync(AdminRole);
        var role = await CreateRoleAsync("Synthetic shared role", "ViewPatients");

        var responses = await Task.WhenAll(
            _admin.PutAsJsonAsync($"{Roles}/{role.Id}", Update(role, "ViewPatients", "AttachFiles")),
            second.Client.PutAsJsonAsync($"{Roles}/{role.Id}", Update(role, "ViewPatients", "BookAppointments")));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
        var conflict = responses.Single(r => r.StatusCode == HttpStatusCode.Conflict);
        (await conflict.Content.ReadAsStringAsync()).Should().Contain("Another admin changed this role");
        var saved = await (await _admin.GetAsync($"{Roles}/{role.Id}")).ReadAsAsync<RoleResponse>();
        saved.Permissions.Should().HaveCount(2);
        (await ActivityAsync("RoleChanged")).Items.Should().ContainSingle();

        // A save from the version both started from is refused now too.
        await (await _admin.PutAsJsonAsync($"{Roles}/{role.Id}", Update(role, "ViewPatients"))).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task ANameTaken_IgnoringCase_IsAConflict()
    {
        var role = await CreateRoleAsync("Synthetic night team");

        await (await _admin.PostAsJsonAsync(Roles, new CreateRoleRequest { Name = "synthetic NIGHT team", Permissions = [] }))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        var other = await CreateRoleAsync("Synthetic day team");
        var renamed = Update(other);
        renamed.Name = role.Name.ToUpperInvariant();
        await (await _admin.PutAsJsonAsync($"{Roles}/{other.Id}", renamed)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task EachChange_IsEmailedToTheOtherAdmins_NotTheOneWhoMadeIt()
    {
        var mine = AccountSecurity.NewAddress();
        await AccountSecurity.ConfirmAddressAsync(_admin, AdminPassword, mine);
        var second = await CreateStaffAsync(AdminRole);
        var theirs = AccountSecurity.NewAddress();
        await AccountSecurity.ConfirmAddressAsync(second.Client, second.Password, theirs);
        Api.Emails.Clear();

        var role = await CreateRoleAsync("Synthetic outreach", "ViewPatients");
        await (await _admin.PutAsJsonAsync($"{Roles}/{role.Id}", Update(role, "ViewPatients", "AttachFiles"))).ShouldHaveStatusAsync(HttpStatusCode.OK);

        Api.Emails.To(mine).Should().BeEmpty();
        var told = Api.Emails.To(theirs);
        told.Should().HaveCount(2).And.OnlyContain(m => m.Subject == "An administrator changed a MedApp role");
        told.Should().Contain(m => m.TextBody.Contains($"{AdminUsername} changed the role Synthetic outreach: added AttachFiles."));
    }

    [Test]
    public async Task Roles_NeedManageRoles_TheListManageUsers()
    {
        var manager = await CreateStaffAsync("Manager");

        await (await manager.Client.GetAsync(Roles)).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await (await manager.Client.GetAsync("/api/permissions")).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await (await manager.Client.PostAsJsonAsync(Roles, new CreateRoleRequest { Name = "Synthetic", Permissions = [] }))
            .ShouldHaveStatusAsync(HttpStatusCode.Forbidden);

        var catalogue = await (await _admin.GetAsync("/api/permissions")).ReadAsAsync<List<PermissionResponse>>();
        catalogue.Select(p => p.Name).Should().Equal(Permissions.Catalogue.Select(p => p.Name));
    }

    [Test]
    public async Task GivingPeopleAnArchivedRole_IsRefused_ButTheyCanBeGivenItOnceRestored()
    {
        var role = await CreateRoleAsync("Synthetic retired team", "ViewPatients");
        var archived = await (await _admin.PostAsJsonAsync($"{Roles}/{role.Id}/archive", new RoleVersionRequest { ExpectedVersion = role.Version }))
            .ReadAsAsync<RoleResponse>();
        var staff = await CreateStaffAsync("Nurse");

        await (await _admin.PutAsJsonAsync($"/api/auth/users/{staff.Username}/roles", new SetUserRolesRequest(["Nurse", role.Name])))
            .ShouldHaveStatusAsync(HttpStatusCode.BadRequest);

        await (await _admin.PostAsJsonAsync($"{Roles}/{role.Id}/restore", new RoleVersionRequest { ExpectedVersion = archived.Version }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await _admin.PutAsJsonAsync($"/api/auth/users/{staff.Username}/roles", new SetUserRolesRequest(["Nurse", role.Name])))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);
    }
}
