using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Contracts.Common;
using MedApp.Contracts.Patients.Requests;
using MedApp.Contracts.Patients.Responses;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Patients;

/// <summary>Patients through the API, with search running as real SQL.</summary>
[TestFixture]
public sealed class PatientTests : IntegrationTest
{
    private HttpClient _staff = null!;

    [SetUp]
    public async Task SignInAsync() => _staff = (await CreateStaffAsync(UserRole)).Client;

    [Test]
    public async Task CreatePatient_ValidRequest_Returns201WithItsLocation()
    {
        var request = new CreatePatientRequest
        {
            FirstName = "Ada",
            LastName = UniqueName("Synthetic"),
            DateOfBirth = DateOfBirth,
            Email = $"ada@{PatientEmailDomain}",
        };

        var response = await _staff.PostAsJsonAsync("/api/patients", request);

        var created = await response.ReadAsAsync<PatientResponse>(HttpStatusCode.Created);
        created.Should().BeEquivalentTo(new { request.FirstName, request.LastName, request.DateOfBirth, request.Email });
        response.Headers.Location!.AbsolutePath.Should().BeEquivalentTo($"/api/patients/{created.Id}");
        var fetched = await (await _staff.GetAsync(response.Headers.Location)).ReadAsAsync<PatientResponse>();
        fetched.Should().Be(created);
    }

    [Test]
    public async Task CreatePatient_InvalidRequest_Returns400ValidationProblemForEachField()
    {
        var response = await _staff.PostAsJsonAsync("/api/patients", new CreatePatientRequest
        {
            FirstName = "",
            LastName = "Synthetic",
            DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10),
            Email = "not-an-email",
        });

        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest);
        problem.Status.Should().Be(400);
        problem.Errors.Keys.Should().BeEquivalentTo(["FirstName", "DateOfBirth", "Email"]);
    }

    [Test]
    public async Task UpdatePatient_ExistingPatient_SavesTheChanges()
    {
        var patient = await CreatePatientAsync(_staff);
        var update = new UpdatePatientRequest
        {
            FirstName = "Renamed",
            LastName = patient.LastName,
            DateOfBirth = patient.DateOfBirth.AddYears(1),
            Email = $"renamed@{PatientEmailDomain}",
        };

        var updated = await (await _staff.PatchAsJsonAsync($"/api/patients/{patient.Id}", update)).ReadAsAsync<PatientResponse>();

        updated.Should().Be(new PatientResponse(patient.Id, update.FirstName, update.LastName, update.DateOfBirth, update.Email));
        (await (await _staff.GetAsync($"/api/patients/{patient.Id}")).ReadAsAsync<PatientResponse>()).Should().Be(updated);
    }

    [Test]
    public async Task UpdatePatient_UnknownPatient_Returns404()
    {
        var response = await _staff.PatchAsJsonAsync($"/api/patients/{Guid.NewGuid()}", new UpdatePatientRequest
        {
            FirstName = "Nobody",
            LastName = "Synthetic",
            DateOfBirth = DateOfBirth,
            Email = $"nobody@{PatientEmailDomain}",
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeletePatient_NoProcedures_DeletesThem()
    {
        var patient = await CreatePatientAsync(_staff);

        await (await _staff.DeleteAsync($"/api/patients/{patient.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await (await _staff.GetAsync($"/api/patients/{patient.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NotFound);
        await (await _staff.DeleteAsync($"/api/patients/{patient.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeletePatient_WithAProcedure_Returns409AndKeepsThem()
    {
        var admin = await SignInAsAdminAsync();
        var procedure = await CreatePatientWithProcedureAsync(admin, stages: 1);

        var response = await _staff.DeleteAsync($"/api/patients/{procedure.PatientId}");

        var problem = await response.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict);
        problem.Title.Should().NotBeNullOrWhiteSpace();
        await (await _staff.GetAsync($"/api/patients/{procedure.PatientId}")).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task SearchPatients_TextFilters_MatchPartOfTheTextIgnoringCase()
    {
        var family = UniqueName("Fam");
        var match = await CreatePatientAsync(_staff, "Grace", $"{family}-Hopper");
        await CreatePatientAsync(_staff, "Grace", UniqueName("Other"));
        await CreatePatientAsync(_staff, "Alan", $"{family}-Turing");

        var page = await SearchAsync($"firstName=gRAC&lastName={family.ToUpperInvariant()}");

        page.Items.Should().ContainSingle().Which.Id.Should().Be(match.Id);
        page.TotalCount.Should().Be(1);
    }

    [Test]
    public async Task SearchPatients_DateOfBirthRange_IncludesBothEnds()
    {
        var family = UniqueName("Dob");
        var onStart = await CreatePatientAsync(_staff, "Start", family, new DateOnly(1990, 1, 1));
        var onEnd = await CreatePatientAsync(_staff, "End", family, new DateOnly(1990, 12, 31));
        await CreatePatientAsync(_staff, "Before", family, new DateOnly(1989, 12, 31));
        await CreatePatientAsync(_staff, "After", family, new DateOnly(1991, 1, 1));

        var page = await SearchAsync($"lastName={family}&dateOfBirthFrom=1990-01-01&dateOfBirthTo=1990-12-31");

        page.Items.Select(p => p.Id).Should().BeEquivalentTo([onStart.Id, onEnd.Id]);
    }

    [Test]
    public async Task SearchPatients_SortedByDateOfBirthDescending_ReturnsYoungestFirst()
    {
        var family = UniqueName("Sort");
        var oldest = await CreatePatientAsync(_staff, "A", family, new DateOnly(1950, 6, 1));
        var youngest = await CreatePatientAsync(_staff, "B", family, new DateOnly(2001, 6, 1));
        var middle = await CreatePatientAsync(_staff, "C", family, new DateOnly(1975, 6, 1));

        var page = await SearchAsync($"lastName={family}&sort=dateOfBirth&ascending=false");

        page.Items.Select(p => p.Id).Should().Equal(youngest.Id, middle.Id, oldest.Id);
    }

    [Test]
    public async Task SearchPatients_ByDefault_SortsByLastNameThenFirstName()
    {
        var family = UniqueName("Name");
        var b = await CreatePatientAsync(_staff, "Beth", $"{family}-b");
        var a2 = await CreatePatientAsync(_staff, "Zoe", $"{family}-a");
        var a1 = await CreatePatientAsync(_staff, "amy", $"{family}-a");

        var page = await SearchAsync($"lastName={family}");

        page.Items.Select(p => p.Id).Should().Equal(a1.Id, a2.Id, b.Id);
    }

    [Test]
    public async Task SearchPatients_SameNamesOverSeveralPages_ShowsEachPatientExactlyOnce()
    {
        // Identical names and dates of birth: only the ID breaks the tie, so the pages must still split cleanly.
        var family = UniqueName("Twin");
        var ids = new List<Guid>();
        for (var i = 0; i < 7; i++)
            ids.Add((await CreatePatientAsync(_staff, "Same", family)).Id);

        var pages = new List<PagedResponse<PatientResponse>>();
        for (var page = 1; page <= 3; page++)
            pages.Add(await SearchAsync($"lastName={family}&pageSize=3&page={page}"));

        pages.Select(p => p.Items.Count).Should().Equal(3, 3, 1);
        pages.Should().AllSatisfy(p =>
        {
            p.TotalCount.Should().Be(7);
            p.TotalPages.Should().Be(3);
            p.PageSize.Should().Be(3);
        });
        pages.SelectMany(p => p.Items).Select(p => p.Id).Should().BeEquivalentTo(ids).And.OnlyHaveUniqueItems();
    }

    [Test]
    public async Task SearchPatients_PageSizeOverTheMaximum_Returns400ValidationProblem()
    {
        var response = await _staff.GetAsync("/api/patients/search?pageSize=101&sort=height");

        var problem = await response.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest);
        problem.Errors.Keys.Should().BeEquivalentTo(["PageSize", "Sort"]);
    }

    private async Task<PagedResponse<PatientResponse>> SearchAsync(string query) =>
        await (await _staff.GetAsync($"/api/patients/search?{query}")).ReadAsAsync<PagedResponse<PatientResponse>>();
}
