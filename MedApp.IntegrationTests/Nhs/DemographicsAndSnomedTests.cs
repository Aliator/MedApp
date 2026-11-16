using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Contracts.Activity.Responses;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Common;
using MedApp.Contracts.Demographics.Responses;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Requests;
using MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Responses;
using MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Responses;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Nhs;

/// <summary>
/// The demographics (PDS) lookup, off by default and with the offline stub switched on, and SNOMED CT codes on stage
/// definitions, copied onto a procedure's stages.
/// </summary>
[TestFixture]
public sealed class DemographicsAndSnomedTests : IntegrationTest
{
    private const string SnomedCode = "80146002";
    private const string SnomedTerm = "Appendectomy";

    [Test]
    public async Task Demographics_ByDefault_IsOff()
    {
        var staff = (await CreateStaffAsync(UserRole)).Client;

        var status = await (await staff.GetAsync("/api/demographics/status")).ReadAsAsync<DemographicsStatusResponse>();
        var lookUp = await staff.GetAsync("/api/demographics/9990000018");

        status.IsAvailable.Should().BeFalse();
        status.Source.Should().BeNull();
        await lookUp.ShouldHaveStatusAsync(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Demographics_OfflineStub_FindsSyntheticPeople_AndLogsEachLookupWithoutTheNumber()
    {
        var account = await CreateStaffAsync(UserRole);
        await using var offline = Api.WithWebHostBuilder(builder => builder.UseSetting("Pds:Mode", "Offline"));
        var staff = offline.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = MedAppApiFactory.BaseAddress,
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        await (await staff.PostAsJsonAsync("/api/auth/login", new LoginRequest(account.Username, account.Password)))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        var status = await (await staff.GetAsync("/api/demographics/status")).ReadAsAsync<DemographicsStatusResponse>();
        var found = await staff.GetAsync($"/api/demographics/{Uri.EscapeDataString("999 000 0026")}");
        var person = await found.ReadAsAsync<DemographicsLookupResponse>();
        var nobody = await staff.GetAsync("/api/demographics/9990000050");
        var invalid = await staff.GetAsync("/api/demographics/9990000019");

        status.IsAvailable.Should().BeTrue();
        found.Headers.CacheControl!.NoStore.Should().BeTrue();
        person.Should().BeEquivalentTo(new { NhsNumber = "9990000026", FirstName = "Ben", LastName = "Testcase", DateOfBirth = new DateOnly(1955, 11, 2) });
        await nobody.ShouldHaveStatusAsync(HttpStatusCode.NotFound);
        (await invalid.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest)).Errors.Should().ContainKey("NhsNumber");

        var admin = await SignInAsAdminAsync();
        var lookups = (await (await admin.GetAsync("/api/activity/search?PageSize=100&Actions=DemographicsLookedUp"))
            .ReadAsAsync<PagedResponse<ActivityEntryResponse>>()).Items;
        lookups.Select(e => e.Details).Should().BeEquivalentTo(["""{"found":true}""", """{"found":false}"""]);
        lookups.Should().AllSatisfy(e => e.Username.Should().Be(account.Username));
    }

    [Test]
    public async Task SnomedCode_OnADefinition_IsShownOnTheTemplate_AndCopiedOntoNewProcedures()
    {
        var admin = await SignInAsAdminAsync();
        var definition = await (await admin.PostAsJsonAsync("/api/procedures/patient-procedure-stage/definitions",
            new CreatePatientProcedureStageDefinitionRequest
            {
                Name = UniqueName("Coded"),
                Description = "Synthetic coded stage",
                Instructions = "Synthetic instructions",
                SnomedCode = SnomedCode,
                SnomedTerm = SnomedTerm,
            })).ReadAsAsync<PatientProcedureStageDefinitionResponse>(HttpStatusCode.Created);
        var template = await CreateTemplateAsync(admin, definition.Id, (await CreateStageDefinitionAsync(admin)).Id);
        var patient = await CreatePatientAsync(admin);

        var procedure = await CreateProcedureAsync(admin, patient.Id, template.Id);

        definition.SnomedCode.Should().Be(SnomedCode);
        template.Stages[0].Should().BeEquivalentTo(new { SnomedCode, SnomedTerm });
        procedure.Stages[0].Should().BeEquivalentTo(new { SnomedCode, SnomedTerm });
        procedure.Stages[1].SnomedCode.Should().BeNull();
        procedure.Patient!.Id.Should().Be(patient.Id);

        // Removing the code from the definition later leaves the procedure's copy as it was.
        var cleared = await (await admin.PatchAsJsonAsync($"/api/procedures/patient-procedure-stage/definitions/{definition.Id}",
            new UpdatePatientProcedureStageDefinitionRequest { SnomedCode = "" })).ReadAsAsync<PatientProcedureStageDefinitionResponse>();
        var reread = await (await admin.GetAsync(ProcedureUrl(procedure.Id))).ReadAsAsync<PatientProcedureResponse>();

        cleared.SnomedCode.Should().BeNull();
        cleared.SnomedTerm.Should().BeNull();
        reread.Stages[0].SnomedCode.Should().Be(SnomedCode);
    }

    [Test]
    public async Task SnomedCode_NotAConcept_Returns400OnTheField()
    {
        var admin = await SignInAsAdminAsync();

        var response = await admin.PostAsJsonAsync("/api/procedures/patient-procedure-stage/definitions",
            new CreatePatientProcedureStageDefinitionRequest
            {
                Name = UniqueName("Coded"),
                Description = "d",
                Instructions = "i",
                SnomedCode = "80146003",
                SnomedTerm = SnomedTerm,
            });

        var problem = await response.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest);
        problem.Errors.Keys.Should().Equal("SnomedCode");
    }
}
