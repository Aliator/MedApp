using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Contracts.Activity.Responses;
using MedApp.Contracts.Common;
using MedApp.Contracts.Patients.Requests;
using MedApp.Contracts.Patients.Responses;
using MedApp.Domain.Activity;
using MedApp.Domain.Patients;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Nhs;

/// <summary>
/// The NHS number through the API against real SQL Server: validated, stored as its digits, unique (the filtered index
/// included), searchable, exported and removed by anonymisation.
/// </summary>
[TestFixture]
public sealed class NhsNumberTests : IntegrationTest
{
    private HttpClient _staff = null!;

    [SetUp]
    public async Task SignInAsync() => _staff = (await CreateStaffAsync(UserRole)).Client;

    private async Task<HttpResponseMessage> PostPatientAsync(string? nhsNumber, HttpClient? client = null) =>
        await (client ?? _staff).PostAsJsonAsync("/api/patients", new CreatePatientRequest
        {
            FirstName = "Synthetic",
            LastName = UniqueName("Nhs"),
            DateOfBirth = DateOfBirth,
            Email = $"{Guid.NewGuid():N}@{PatientEmailDomain}",
            NhsNumber = nhsNumber,
        });

    [Test]
    public async Task CreatePatient_NumberWithSpaces_StoresAndReturnsItsDigits()
    {
        var created = await (await PostPatientAsync(NhsNumberWithSpaces)).ReadAsAsync<PatientResponse>(HttpStatusCode.Created);

        created.NhsNumber.Should().Be(NhsNumber);
        (await (await _staff.GetAsync($"/api/patients/{created.Id}")).ReadAsAsync<PatientResponse>()).NhsNumber.Should().Be(NhsNumber);
    }

    [Test]
    public async Task CreatePatient_ManyWithoutANumber_AreAllowed()
    {
        // The unique index is filtered, so any number of patients can have no NHS number.
        for (var i = 0; i < 3; i++)
            (await (await PostPatientAsync(null)).ReadAsAsync<PatientResponse>(HttpStatusCode.Created)).NhsNumber.Should().BeNull();
    }

    [Test]
    public async Task CreatePatient_WrongCheckDigit_Returns400OnTheField()
    {
        var problem = await (await PostPatientAsync("999 000 0019")).ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest);

        problem.Errors.Should().ContainKey("NhsNumber").WhoseValue.Should().Equal(NhsNumberRules.WrongCheckDigit);
    }

    [Test]
    public async Task CreatePatient_Placeholder_Returns400WithItsOwnMessage()
    {
        var problem = await (await PostPatientAsync("111 111 1111")).ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest);

        problem.Errors.Should().ContainKey("NhsNumber").WhoseValue.Should().Equal(NhsNumberRules.Placeholder);
    }

    [Test]
    public void Database_FailedSavesAndCommands_AreLoggedAtDebug_SoAValueInTheirMessageStaysOutOfTheLog()
    {
        // The NHS number's unique index puts the number in SQL Server's message; EF Core's own failure logs carry it.
        using var scope = Api.Services.CreateScope();
        var warnings = scope.ServiceProvider.GetRequiredService<MedApp.Infrastructure.Data.MedAppDbContext>()
            .GetService<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptions>()
            .FindExtension<Microsoft.EntityFrameworkCore.Infrastructure.CoreOptionsExtension>()!
            .WarningsConfiguration;

        warnings.GetLevel(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.SaveChangesFailed)
            .Should().Be(Microsoft.Extensions.Logging.LogLevel.Debug);
        warnings.GetLevel(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandError)
            .Should().Be(Microsoft.Extensions.Logging.LogLevel.Debug);
    }

    [Test]
    public async Task CreatePatient_AnotherPatientHasTheNumber_Returns409()
    {
        await (await PostPatientAsync(NhsNumber)).ShouldHaveStatusAsync(HttpStatusCode.Created);

        var problem = await (await PostPatientAsync(NhsNumberWithSpaces)).ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict);

        problem.Title.Should().Be(NhsNumberRules.InUse);
    }

    [Test]
    public async Task Repository_DuplicatePastTheCheck_IsAConflictFromTheUniqueIndex()
    {
        // As if two saves raced past the handler's check: the database's index refuses the second.
        await (await PostPatientAsync(NhsNumber)).ShouldHaveStatusAsync(HttpStatusCode.Created);

        var act = () => RunInScopeAsync(async services =>
        {
            var patient = new Patient
            {
                Id = Guid.NewGuid(),
                FirstName = "Synthetic",
                LastName = "Racer",
                DateOfBirth = DateOfBirth,
                Email = $"racer@{PatientEmailDomain}",
                NhsNumber = NhsNumber,
                CreatedAt = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow,
            };
            var log = services.GetRequiredService<IActivityLog>();
            await services.GetRequiredService<IPatientRepository>()
                .AddAsync(patient, log.Entry(ActivityAction.PatientCreated, patient.Id), CancellationToken.None);
        });

        await act.Should().ThrowAsync<ConflictException>().WithMessage(NhsNumberRules.InUse);
    }

    [Test]
    public async Task UpdatePatient_ChangedRemovedAndLeftAlone()
    {
        var patient = await (await PostPatientAsync(NhsNumber)).ReadAsAsync<PatientResponse>(HttpStatusCode.Created);
        var url = $"/api/patients/{patient.Id}";

        var leftAlone = await (await _staff.PatchAsJsonAsync(url, Edit(patient, null))).ReadAsAsync<PatientResponse>();
        var changed = await (await _staff.PatchAsJsonAsync(url, Edit(patient, OtherNhsNumber))).ReadAsAsync<PatientResponse>();
        var removed = await (await _staff.PatchAsJsonAsync(url, Edit(patient, ""))).ReadAsAsync<PatientResponse>();

        leftAlone.NhsNumber.Should().Be(NhsNumber);
        changed.NhsNumber.Should().Be(OtherNhsNumber);
        removed.NhsNumber.Should().BeNull();
    }

    [Test]
    public async Task UpdatePatient_ToAnotherPatientsNumber_Returns409AndKeepsTheirOwn()
    {
        await (await PostPatientAsync(OtherNhsNumber)).ShouldHaveStatusAsync(HttpStatusCode.Created);
        var patient = await (await PostPatientAsync(NhsNumber)).ReadAsAsync<PatientResponse>(HttpStatusCode.Created);

        await (await _staff.PatchAsJsonAsync($"/api/patients/{patient.Id}", Edit(patient, OtherNhsNumber)))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);

        (await (await _staff.GetAsync($"/api/patients/{patient.Id}")).ReadAsAsync<PatientResponse>()).NhsNumber.Should().Be(NhsNumber);
    }

    [Test]
    public async Task SearchPatients_ByNhsNumber_MatchesTheDigitsInSql_AndLogsTheFilterNotTheNumber()
    {
        var match = await (await PostPatientAsync(NhsNumber)).ReadAsAsync<PatientResponse>(HttpStatusCode.Created);
        await (await PostPatientAsync(OtherNhsNumber)).ShouldHaveStatusAsync(HttpStatusCode.Created);
        await (await PostPatientAsync(null)).ShouldHaveStatusAsync(HttpStatusCode.Created);

        var page = await (await _staff.GetAsync($"/api/patients/search?nhsNumber={Uri.EscapeDataString("999 000 0018")}"))
            .ReadAsAsync<PagedResponse<PatientResponse>>();
        var partial = await (await _staff.GetAsync("/api/patients/search?nhsNumber=99900000"))
            .ReadAsAsync<PagedResponse<PatientResponse>>();

        page.Items.Should().ContainSingle().Which.Id.Should().Be(match.Id);
        partial.TotalCount.Should().Be(2);
        var admin = await SignInAsAdminAsync();
        var searches = await (await admin.GetAsync("/api/activity/search?PageSize=100&Actions=PatientsSearched"))
            .ReadAsAsync<PagedResponse<ActivityEntryResponse>>();
        searches.Items.Should().NotBeEmpty().And.AllSatisfy(e =>
        {
            e.Details.Should().Contain("nhsNumber").And.NotContain(NhsNumber).And.NotContain("99900000");
        });
    }

    [Test]
    public async Task ExportAndAnonymise_TheNumberIsExported_ThenRemoved_AndFreeForAnother()
    {
        var admin = await SignInAsAdminAsync();
        var patient = await (await PostPatientAsync(NhsNumber, admin)).ReadAsAsync<PatientResponse>(HttpStatusCode.Created);

        var export = await (await admin.GetAsync($"/api/patients/{patient.Id}/export")).ReadAsAsync<PatientExportResponse>();
        export.Patient.NhsNumber.Should().Be(NhsNumber);

        var anonymised = await (await admin.PostAsJsonAsync($"/api/patients/{patient.Id}/anonymise", new AnonymisePatientRequest
        {
            Reason = "Synthetic: past its retention period",
            Confirmation = $"{patient.FirstName} {patient.LastName}",
        })).ReadAsAsync<PatientResponse>();

        anonymised.NhsNumber.Should().BeNull();
        await (await PostPatientAsync(NhsNumber)).ShouldHaveStatusAsync(HttpStatusCode.Created);
    }

    /// <summary>An edit of the patient's details as the form sends them (every field), with this NHS number.</summary>
    private static UpdatePatientRequest Edit(PatientResponse patient, string? nhsNumber) => new()
    {
        FirstName = patient.FirstName,
        LastName = patient.LastName,
        DateOfBirth = patient.DateOfBirth,
        Email = patient.Email,
        NhsNumber = nhsNumber,
    };

    private const string NhsNumber = "9990000018";
    private const string NhsNumberWithSpaces = "999 000 0018";
    private const string OtherNhsNumber = "9990000026";
}
