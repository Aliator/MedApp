using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Contracts.Procedures.PatientProcedures.Requests;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Api;

/// <summary>How the whole pipeline reports problems: validation, unknown API paths and missing records.</summary>
[TestFixture]
public sealed class ErrorTests : IntegrationTest
{
    private HttpClient _staff = null!;

    [SetUp]
    public async Task SignInAsync() => _staff = (await CreateStaffAsync(UserRole)).Client;

    [Test]
    public async Task Command_InvalidRequest_Returns400ValidationProblemDetails()
    {
        var response = await _staff.PostAsJsonAsync("/api/procedures/patient-procedures", new CreatePatientProcedureRequest
        {
            PatientId = Guid.Empty,
            TemplateId = Guid.Empty,
            DueDate = UkToday,
            Priority = "Whenever",
        });

        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest);
        problem.Title.Should().Be("One or more validation errors occurred.");
        problem.Status.Should().Be(400);
        problem.Errors.Keys.Should().BeEquivalentTo(["PatientId", "TemplateId", "Priority"]);
        problem.Errors["Priority"].Should().ContainSingle().Which.Should().Contain("Urgent");
    }

    [Test]
    public async Task Query_InvalidPaging_Returns400ValidationProblemDetails()
    {
        var response = await _staff.GetAsync("/api/procedures/patient-procedures/search?page=0");

        var problem = await response.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainKey("Page");
    }

    [Test]
    public async Task Request_UnreadableJson_Returns400()
    {
        var response = await _staff.PostAsync("/api/patients",
            new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json"));

        await response.ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
    }

    [TestCase("/api/no-such-endpoint")]
    [TestCase("/api/patients/not-a-guid")]
    [TestCase("/api/procedures/nothing/here")]
    public async Task ApiPath_MatchingNoEndpoint_Returns404NotTheApp(string path)
    {
        var response = await _staff.GetAsync(path);

        await response.ShouldHaveStatusAsync(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().NotContain(MedAppApiFactory.StubAppMarker);
    }

    [Test]
    public async Task ApiPath_MatchingNoEndpointSignedOut_Returns404()
    {
        var response = await NewClient().GetAsync("/api/no-such-endpoint");

        await response.ShouldHaveStatusAsync(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().NotContain(MedAppApiFactory.StubAppMarker);
    }

    /// <summary>
    /// The other side of the 404s above: any other path is one of the app's pages, so the app is there to fall back
    /// to, and only the <c>/api</c> route keeps API paths from getting it.
    /// </summary>
    [TestCase("/patients/anything")]
    [TestCase("/procedures/00000000-0000-0000-0000-000000000000")]
    public async Task PagePath_AnyRoute_ReturnsTheApp(string path)
    {
        var response = await NewClient().GetAsync(path);

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        (await response.Content.ReadAsStringAsync()).Should().Contain(MedAppApiFactory.StubAppMarker);
    }

    [Test]
    public async Task Query_UnknownRecord_Returns404ProblemDetails()
    {
        var response = await _staff.GetAsync($"/api/procedures/patient-procedures/{Guid.NewGuid()}");

        var problem = await response.ReadAsAsync<ProblemDetails>(HttpStatusCode.NotFound);
        problem.Title.Should().Contain("was not found");
    }
}
