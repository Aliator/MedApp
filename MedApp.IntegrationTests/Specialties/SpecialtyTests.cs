using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Contracts.Activity.Responses;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Common;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Requests;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MedApp.Contracts.Specialties.Requests;
using MedApp.Contracts.Specialties.Responses;
using MedApp.Domain.Specialties;
using MedApp.IntegrationTests.Common;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Specialties;

/// <summary>
/// Specialties through the API against SQL Server: the admins' list (the unique name, versions, archiving and the
/// foreign keys that keep one in use from being deleted), a template's specialty and each pathway's own copy of it,
/// and staff memberships with their activity log entry and their cascade with the account.
/// </summary>
[TestFixture]
public sealed class SpecialtyTests : IntegrationTest
{
    private const string Specialties = "/api/specialties";
    private const string Templates = "/api/pathways/patient-pathway-stage/templates";

    private HttpClient _admin = null!;

    [SetUp]
    public async Task SignInAsync() => _admin = await SignInAsAdminAsync();

    private async Task<SpecialtyResponse> CreateSpecialtyAsync(string? name = null) =>
        await (await _admin.PostAsJsonAsync(Specialties, new CreateSpecialtyRequest { Name = name ?? UniqueName("Specialty") }))
            .ReadAsAsync<SpecialtyResponse>(HttpStatusCode.Created);

    private async Task<SpecialtyResponse> GetSpecialtyAsync(Guid id) =>
        await (await _admin.GetAsync($"{Specialties}/{id}")).ReadAsAsync<SpecialtyResponse>();

    private async Task<PathwayTemplateResponse> CreateTemplateInAsync(Guid specialtyId)
    {
        var stage = await CreateStageDefinitionAsync(_admin);
        return await (await _admin.PostAsJsonAsync(Templates, new CreatePathwayTemplateRequest
            {
                Name = UniqueName("Template"),
                SpecialtyId = specialtyId,
                StageDefinitionIdsInOrder = [stage.Id],
            }))
            .ReadAsAsync<PathwayTemplateResponse>(HttpStatusCode.Created);
    }

    [Test]
    public async Task General_IsThereFromTheMigrations()
    {
        var all = await (await _admin.GetAsync(Specialties)).ReadAsAsync<List<SpecialtyResponse>>();

        all.Should().ContainSingle(s => s.Id == Specialty.GeneralId).Which.Name.Should().Be(Specialty.GeneralName);
    }

    [Test]
    public async Task Admin_AddsRenamesArchivesRestoresAndDeletesOne()
    {
        var created = await CreateSpecialtyAsync();
        created.Version.Should().Be(1);
        created.CanBeDeleted.Should().BeTrue();

        // A name another has, in any case, is refused.
        await (await _admin.PostAsJsonAsync(Specialties, new CreateSpecialtyRequest { Name = created.Name.ToUpperInvariant() }))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);

        var newName = UniqueName("Renamed");
        var renamed = await (await _admin.PutAsJsonAsync($"{Specialties}/{created.Id}",
                new UpdateSpecialtyRequest { Name = newName, ExpectedVersion = 1 }))
            .ReadAsAsync<SpecialtyResponse>();
        renamed.Name.Should().Be(newName);
        renamed.Version.Should().Be(2);

        // A page still on version 1 is told someone changed it.
        await (await _admin.PostAsJsonAsync($"{Specialties}/{created.Id}/archive", new SpecialtyVersionRequest { ExpectedVersion = 1 }))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);

        var archived = await (await _admin.PostAsJsonAsync($"{Specialties}/{created.Id}/archive", new SpecialtyVersionRequest { ExpectedVersion = 2 }))
            .ReadAsAsync<SpecialtyResponse>();
        archived.IsArchived.Should().BeTrue();
        (await (await _admin.GetAsync(Specialties)).ReadAsAsync<List<SpecialtyResponse>>()).Should().NotContain(s => s.Id == created.Id);
        (await (await _admin.GetAsync($"{Specialties}?includeArchived=true")).ReadAsAsync<List<SpecialtyResponse>>())
            .Should().Contain(s => s.Id == created.Id);

        var restored = await (await _admin.PostAsJsonAsync($"{Specialties}/{created.Id}/restore", new SpecialtyVersionRequest { ExpectedVersion = 3 }))
            .ReadAsAsync<SpecialtyResponse>();
        restored.IsArchived.Should().BeFalse();
        restored.Version.Should().Be(4);

        await (await _admin.DeleteAsync($"{Specialties}/{created.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        await (await _admin.GetAsync($"{Specialties}/{created.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Staff_ReadThemButCantChangeThem()
    {
        var staff = await CreateStaffAsync(UserRole);

        await (await staff.Client.GetAsync(Specialties)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await staff.Client.PostAsJsonAsync(Specialties, new CreateSpecialtyRequest { Name = UniqueName("Specialty") }))
            .ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await (await staff.Client.PutAsJsonAsync($"/api/auth/users/{staff.Username}/specialties",
                new SetUserSpecialtiesRequest([Specialty.GeneralId])))
            .ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Pathway_CopiesItsTemplatesSpecialty_AndKeepsItWhenTheTemplateMoves()
    {
        var dermatology = await CreateSpecialtyAsync();
        var urology = await CreateSpecialtyAsync();
        var template = await CreateTemplateInAsync(dermatology.Id);
        template.SpecialtyId.Should().Be(dermatology.Id);
        template.SpecialtyName.Should().Be(dermatology.Name);
        var patient = await CreatePatientAsync(_admin);

        var before = await CreatePathwayAsync(_admin, patient.Id, template.Id);
        before.SpecialtyId.Should().Be(dermatology.Id);

        var moved = await (await _admin.PatchAsJsonAsync($"{Templates}/{template.Id}", new UpdatePathwayTemplateRequest { SpecialtyId = urology.Id }))
            .ReadAsAsync<PathwayTemplateResponse>();
        moved.SpecialtyName.Should().Be(urology.Name);
        var after = await CreatePathwayAsync(_admin, patient.Id, template.Id);

        (await (await _admin.GetAsync($"/api/pathways/patient-pathways/{before.Id}")).ReadAsAsync<PatientPathwayResponse>())
            .SpecialtyId.Should().Be(dermatology.Id);
        after.SpecialtyId.Should().Be(urology.Id);
        (await GetSpecialtyAsync(dermatology.Id)).Should().BeEquivalentTo(new { TemplateCount = 0, PathwayCount = 1, CanBeDeleted = false });
        (await GetSpecialtyAsync(urology.Id)).Should().BeEquivalentTo(new { TemplateCount = 1, PathwayCount = 1, CanBeDeleted = false });

        // In use, so it can only be archived.
        await (await _admin.DeleteAsync($"{Specialties}/{dermatology.Id}")).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task ArchivedSpecialty_CantBeChosenForATemplate_ButATemplateInItKeepsIt()
    {
        var specialty = await CreateSpecialtyAsync();
        var template = await CreateTemplateInAsync(specialty.Id);
        await (await _admin.PostAsJsonAsync($"{Specialties}/{specialty.Id}/archive", new SpecialtyVersionRequest { ExpectedVersion = 1 }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        var stage = await CreateStageDefinitionAsync(_admin);

        await (await _admin.PostAsJsonAsync(Templates, new CreatePathwayTemplateRequest
            {
                Name = UniqueName("Template"),
                SpecialtyId = specialty.Id,
                StageDefinitionIdsInOrder = [stage.Id],
            }))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        var renamed = await (await _admin.PatchAsJsonAsync($"{Templates}/{template.Id}",
                new UpdatePathwayTemplateRequest { Name = UniqueName("Renamed"), SpecialtyId = specialty.Id }))
            .ReadAsAsync<PathwayTemplateResponse>();
        renamed.SpecialtyId.Should().Be(specialty.Id);

        // No specialty at all is a 400.
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
        var dermatology = await CreateSpecialtyAsync();
        var urology = await CreateSpecialtyAsync();
        var staff = await CreateStaffAsync(UserRole);

        var set = await (await _admin.PutAsJsonAsync($"/api/auth/users/{staff.Username}/specialties",
                new SetUserSpecialtiesRequest([urology.Id, dermatology.Id])))
            .ReadAsAsync<List<StaffSpecialtyResponse>>();
        set.Select(s => s.Id).Should().BeEquivalentTo([dermatology.Id, urology.Id]);
        (await (await _admin.GetAsync($"/api/auth/users/{staff.Username}/specialties")).ReadAsAsync<List<StaffSpecialtyResponse>>())
            .Should().HaveCount(2);
        (await (await staff.Client.GetAsync("/api/auth/me/specialties")).ReadAsAsync<List<StaffSpecialtyResponse>>())
            .Select(s => s.Id).Should().BeEquivalentTo([dermatology.Id, urology.Id]);
        (await GetSpecialtyAsync(urology.Id)).Should().BeEquivalentTo(new { MemberCount = 1, CanBeDeleted = false });

        // Down to one: recorded, by the admin, about the account, with no more than the specialties.
        await (await _admin.PutAsJsonAsync($"/api/auth/users/{staff.Username}/specialties", new SetUserSpecialtiesRequest([dermatology.Id])))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        var entries = await (await _admin.GetAsync("/api/activity/search?PageSize=100&Actions=StaffSpecialtiesChanged"))
            .ReadAsAsync<PagedResponse<ActivityEntryResponse>>();
        entries.Items.Should().HaveCount(2).And.OnlyContain(e => e.Username == AdminUsername && e.TargetUsername == staff.Username);
        entries.Items.Should().Contain(e => e.Details.Contains(urology.Name) && e.Details.Contains(dermatology.Name));

        // An archived one can be kept, not newly given; an unknown one is a 404; an unknown account too.
        await (await _admin.PostAsJsonAsync($"{Specialties}/{urology.Id}/archive", new SpecialtyVersionRequest { ExpectedVersion = 1 }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await _admin.PutAsJsonAsync($"/api/auth/users/{staff.Username}/specialties", new SetUserSpecialtiesRequest([dermatology.Id, urology.Id])))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await (await _admin.PutAsJsonAsync($"/api/auth/users/{staff.Username}/specialties", new SetUserSpecialtiesRequest([Guid.NewGuid()])))
            .ShouldHaveStatusAsync(HttpStatusCode.NotFound);
        await (await _admin.GetAsync("/api/auth/users/nobody-at-all/specialties")).ShouldHaveStatusAsync(HttpStatusCode.NotFound);

        // Deleting the account takes its memberships with it, so the specialty can go.
        await (await _admin.DeleteAsJsonAsync($"/api/auth/users/{staff.Username}", new DeleteUserRequest(AdminPassword)))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        (await GetSpecialtyAsync(dermatology.Id)).MemberCount.Should().Be(0);
        await (await _admin.DeleteAsync($"{Specialties}/{dermatology.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
    }
}
