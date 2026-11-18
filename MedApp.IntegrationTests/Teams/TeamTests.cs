using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Contracts.Activity.Responses;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Common;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Requests;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MedApp.Contracts.Teams.Requests;
using MedApp.Contracts.Teams.Responses;
using MedApp.Domain.Teams;
using MedApp.IntegrationTests.Common;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Teams;

/// <summary>
/// Teams through the API against SQL Server: the admins' list (the unique name, versions, archiving and the
/// foreign keys that keep one in use from being deleted), a template's team and each pathway's own copy of it,
/// and staff memberships with their activity log entry and their cascade with the account.
/// </summary>
[TestFixture]
public sealed class TeamTests : IntegrationTest
{
    private const string Teams = "/api/teams";
    private const string Templates = "/api/pathways/patient-pathway-stage/templates";

    private HttpClient _admin = null!;

    [SetUp]
    public async Task SignInAsync() => _admin = await SignInAsAdminAsync();

    private async Task<TeamResponse> CreateTeamAsync(string? name = null) =>
        await (await _admin.PostAsJsonAsync(Teams, new CreateTeamRequest { Name = name ?? UniqueName("Team") }))
            .ReadAsAsync<TeamResponse>(HttpStatusCode.Created);

    private async Task<TeamResponse> GetTeamAsync(Guid id) =>
        await (await _admin.GetAsync($"{Teams}/{id}")).ReadAsAsync<TeamResponse>();

    private async Task<PathwayTemplateResponse> CreateTemplateInAsync(Guid teamId)
    {
        var stage = await CreateStageDefinitionAsync(_admin);
        return await (await _admin.PostAsJsonAsync(Templates, new CreatePathwayTemplateRequest
            {
                Name = UniqueName("Template"),
                TeamId = teamId,
                StageDefinitionIdsInOrder = [stage.Id],
            }))
            .ReadAsAsync<PathwayTemplateResponse>(HttpStatusCode.Created);
    }

    [Test]
    public async Task General_IsThereFromTheMigrations()
    {
        var all = await (await _admin.GetAsync(Teams)).ReadAsAsync<List<TeamResponse>>();

        all.Should().ContainSingle(s => s.Id == Team.GeneralId).Which.Name.Should().Be(Team.GeneralName);
    }

    [Test]
    public async Task Admin_AddsRenamesArchivesRestoresAndDeletesOne()
    {
        var created = await CreateTeamAsync();
        created.Version.Should().Be(1);
        created.CanBeDeleted.Should().BeTrue();

        // A name another has, in any case, is refused.
        await (await _admin.PostAsJsonAsync(Teams, new CreateTeamRequest { Name = created.Name.ToUpperInvariant() }))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);

        var newName = UniqueName("Renamed");
        var renamed = await (await _admin.PutAsJsonAsync($"{Teams}/{created.Id}",
                new UpdateTeamRequest { Name = newName, ExpectedVersion = 1 }))
            .ReadAsAsync<TeamResponse>();
        renamed.Name.Should().Be(newName);
        renamed.Version.Should().Be(2);

        // A page still on version 1 is told someone changed it.
        await (await _admin.PostAsJsonAsync($"{Teams}/{created.Id}/archive", new TeamVersionRequest { ExpectedVersion = 1 }))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);

        var archived = await (await _admin.PostAsJsonAsync($"{Teams}/{created.Id}/archive", new TeamVersionRequest { ExpectedVersion = 2 }))
            .ReadAsAsync<TeamResponse>();
        archived.IsArchived.Should().BeTrue();
        (await (await _admin.GetAsync(Teams)).ReadAsAsync<List<TeamResponse>>()).Should().NotContain(s => s.Id == created.Id);
        (await (await _admin.GetAsync($"{Teams}?includeArchived=true")).ReadAsAsync<List<TeamResponse>>())
            .Should().Contain(s => s.Id == created.Id);

        var restored = await (await _admin.PostAsJsonAsync($"{Teams}/{created.Id}/restore", new TeamVersionRequest { ExpectedVersion = 3 }))
            .ReadAsAsync<TeamResponse>();
        restored.IsArchived.Should().BeFalse();
        restored.Version.Should().Be(4);

        await (await _admin.DeleteAsync($"{Teams}/{created.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        await (await _admin.GetAsync($"{Teams}/{created.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Staff_ReadThemButCantChangeThem()
    {
        var staff = await CreateStaffAsync(UserRole);

        await (await staff.Client.GetAsync(Teams)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await staff.Client.PostAsJsonAsync(Teams, new CreateTeamRequest { Name = UniqueName("Team") }))
            .ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await (await staff.Client.PutAsJsonAsync($"/api/auth/users/{staff.Username}/teams",
                new SetUserTeamsRequest([Team.GeneralId])))
            .ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Pathway_CopiesItsTemplatesTeam_AndKeepsItWhenTheTemplateMoves()
    {
        var dermatology = await CreateTeamAsync();
        var urology = await CreateTeamAsync();
        var template = await CreateTemplateInAsync(dermatology.Id);
        template.TeamId.Should().Be(dermatology.Id);
        template.TeamName.Should().Be(dermatology.Name);
        var patient = await CreatePatientAsync(_admin);

        var before = await CreatePathwayAsync(_admin, patient.Id, template.Id);
        before.TeamId.Should().Be(dermatology.Id);

        var moved = await (await _admin.PatchAsJsonAsync($"{Templates}/{template.Id}", new UpdatePathwayTemplateRequest { TeamId = urology.Id }))
            .ReadAsAsync<PathwayTemplateResponse>();
        moved.TeamName.Should().Be(urology.Name);
        var after = await CreatePathwayAsync(_admin, patient.Id, template.Id);

        (await (await _admin.GetAsync($"/api/pathways/patient-pathways/{before.Id}")).ReadAsAsync<PatientPathwayResponse>())
            .TeamId.Should().Be(dermatology.Id);
        after.TeamId.Should().Be(urology.Id);
        (await GetTeamAsync(dermatology.Id)).Should().BeEquivalentTo(new { TemplateCount = 0, PathwayCount = 1, CanBeDeleted = false });
        (await GetTeamAsync(urology.Id)).Should().BeEquivalentTo(new { TemplateCount = 1, PathwayCount = 1, CanBeDeleted = false });

        // In use, so it can only be archived.
        await (await _admin.DeleteAsync($"{Teams}/{dermatology.Id}")).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task ArchivedTeam_CantBeChosenForATemplate_ButATemplateInItKeepsIt()
    {
        var team = await CreateTeamAsync();
        var template = await CreateTemplateInAsync(team.Id);
        await (await _admin.PostAsJsonAsync($"{Teams}/{team.Id}/archive", new TeamVersionRequest { ExpectedVersion = 1 }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        var stage = await CreateStageDefinitionAsync(_admin);

        await (await _admin.PostAsJsonAsync(Templates, new CreatePathwayTemplateRequest
            {
                Name = UniqueName("Template"),
                TeamId = team.Id,
                StageDefinitionIdsInOrder = [stage.Id],
            }))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        var renamed = await (await _admin.PatchAsJsonAsync($"{Templates}/{template.Id}",
                new UpdatePathwayTemplateRequest { Name = UniqueName("Renamed"), TeamId = team.Id }))
            .ReadAsAsync<PathwayTemplateResponse>();
        renamed.TeamId.Should().Be(team.Id);

        // No team at all is a 400.
        await (await _admin.PostAsJsonAsync(Templates, new CreatePathwayTemplateRequest
            {
                Name = UniqueName("Template"),
                StageDefinitionIdsInOrder = [stage.Id],
            }))
            .ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Membership_IsSetByAnAdmin_Logged_ReadByItsHolder_AndGoesWithTheAccount()
    {
        var dermatology = await CreateTeamAsync();
        var urology = await CreateTeamAsync();
        var staff = await CreateStaffAsync(UserRole);

        var set = await (await _admin.PutAsJsonAsync($"/api/auth/users/{staff.Username}/teams",
                new SetUserTeamsRequest([urology.Id, dermatology.Id])))
            .ReadAsAsync<List<TeamMembershipResponse>>();
        set.Select(s => s.Id).Should().BeEquivalentTo([dermatology.Id, urology.Id]);
        (await (await _admin.GetAsync($"/api/auth/users/{staff.Username}/teams")).ReadAsAsync<List<TeamMembershipResponse>>())
            .Should().HaveCount(2);
        (await (await staff.Client.GetAsync("/api/auth/me/teams")).ReadAsAsync<List<TeamMembershipResponse>>())
            .Select(s => s.Id).Should().BeEquivalentTo([dermatology.Id, urology.Id]);
        (await GetTeamAsync(urology.Id)).Should().BeEquivalentTo(new { MemberCount = 1, CanBeDeleted = false });

        // Down to one: recorded, by the admin, about the account, with no more than the teams.
        await (await _admin.PutAsJsonAsync($"/api/auth/users/{staff.Username}/teams", new SetUserTeamsRequest([dermatology.Id])))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        var entries = await (await _admin.GetAsync("/api/activity/search?PageSize=100&Actions=TeamMembershipChanged"))
            .ReadAsAsync<PagedResponse<ActivityEntryResponse>>();
        entries.Items.Should().HaveCount(2).And.OnlyContain(e => e.Username == AdminUsername && e.TargetUsername == staff.Username);
        entries.Items.Should().Contain(e => e.Details.Contains(urology.Name) && e.Details.Contains(dermatology.Name));

        // An archived one can be kept, not newly given; an unknown one is a 404; an unknown account too.
        await (await _admin.PostAsJsonAsync($"{Teams}/{urology.Id}/archive", new TeamVersionRequest { ExpectedVersion = 1 }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await _admin.PutAsJsonAsync($"/api/auth/users/{staff.Username}/teams", new SetUserTeamsRequest([dermatology.Id, urology.Id])))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await (await _admin.PutAsJsonAsync($"/api/auth/users/{staff.Username}/teams", new SetUserTeamsRequest([Guid.NewGuid()])))
            .ShouldHaveStatusAsync(HttpStatusCode.NotFound);
        await (await _admin.GetAsync("/api/auth/users/nobody-at-all/teams")).ShouldHaveStatusAsync(HttpStatusCode.NotFound);

        // Deleting the account takes its memberships with it, so the team can go.
        await (await _admin.DeleteAsJsonAsync($"/api/auth/users/{staff.Username}", new DeleteUserRequest(AdminPassword)))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        (await GetTeamAsync(dermatology.Id)).MemberCount.Should().Be(0);
        await (await _admin.DeleteAsync($"{Teams}/{dermatology.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
    }
}
