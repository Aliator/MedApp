using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Contracts.Patients.Requests;
using MedApp.Contracts.Patients.Responses;
using MedApp.Contracts.Procedures.PatientProcedures.Requests;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Requests;
using MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Responses;
using MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Requests;
using MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Responses;
using MedApp.Application.Procedures.Common;
using MedApp.Infrastructure.Common.Identity;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Common;

/// <summary>A signed-in client and who it is.</summary>
public sealed record StaffAccount(Guid UserId, string Username, string Password, HttpClient Client);

/// <summary>
/// Base class for the integration tests. Before each test the database is emptied and the first admin is made, by
/// the same <see cref="FirstAdminSetup"/> a deploy runs. Helpers sign in and make the data a test needs, all through
/// the API.
/// </summary>
public abstract class IntegrationTest
{
    protected static MedAppApiFactory Api => TestEnvironment.Api;

    /// <summary>Whether to make the first admin (and the Admin and User roles) before each test.</summary>
    protected virtual bool MakesFirstAdmin => true;

    [SetUp]
    public async Task ResetAsync()
    {
        await TestEnvironment.ResetDatabaseAsync();

        if (MakesFirstAdmin)
        {
            var result = await RunInScopeAsync(services =>
                services.GetRequiredService<FirstAdminSetup>().RunAsync(AdminUsername, AdminPassword, CancellationToken.None));
            result.Outcome.Should().Be(FirstAdminOutcome.Created);
        }
    }

    /// <summary>Runs something with the API's own services, in a scope of its own (as a request would).</summary>
    protected static async Task<T> RunInScopeAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = Api.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider);
    }

    protected static Task RunInScopeAsync(Func<IServiceProvider, Task> work) =>
        RunInScopeAsync(async services =>
        {
            await work(services);
            return true;
        });

    /// <summary>Runs SQL against the test database, for what the API can't do (e.g. make a session expire).</summary>
    protected static Task<int> ExecuteSqlAsync(FormattableString sql) =>
        RunInScopeAsync(services => services.GetRequiredService<MedAppDbContext>().Database.ExecuteSqlInterpolatedAsync(sql));

    protected static HttpClient NewClient() => Api.CreateApiClient();

    protected static async Task<HttpClient> SignInAsync(string username, string password)
    {
        var client = NewClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));
        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        return client;
    }

    protected static Task<HttpClient> SignInAsAdminAsync() => SignInAsync(AdminUsername, AdminPassword);

    protected static string UniqueName(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 13)];

    /// <summary>
    /// Makes an account through the admin API, with the given roles, then has its user swap the generated password
    /// for one of their own before anything else (as they'll have to once a first password must be changed), and signs
    /// them in again with it.
    /// </summary>
    protected static async Task<StaffAccount> CreateStaffAsync(params string[] roles)
    {
        var admin = await SignInAsAdminAsync();
        var username = UniqueName("staff");

        var created = await (await admin.PostAsJsonAsync("/api/auth/users", new CreateUserRequest(username)))
            .ReadAsAsync<CreateUserResponse>();
        (await admin.PutAsJsonAsync($"/api/auth/users/{username}/roles", new SetUserRolesRequest(roles)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        if (roles.Length > 0)
        {
            var firstSession = await SignInAsync(username, created.Password);
            await (await firstSession.PutAsJsonAsync(
                    $"/api/auth/users/{username}/update-password",
                    new UpdateUserPasswordRequest(created.Password, ChosenPassword)))
                .ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        }

        var password = roles.Length > 0 ? ChosenPassword : created.Password;
        var user = await (await admin.GetAsync($"/api/auth/users/{username}")).ReadAsAsync<UserResponse>();
        return new StaffAccount(user.UserId, username, password, await SignInAsync(username, password));
    }

    protected static async Task<Guid> GetUserIdAsync(HttpClient admin, string username) =>
        (await (await admin.GetAsync($"/api/auth/users/{username}")).ReadAsAsync<UserResponse>()).UserId;

    protected static async Task<PatientResponse> CreatePatientAsync(
        HttpClient client,
        string? firstName = null,
        string? lastName = null,
        DateOnly? dateOfBirth = null)
    {
        firstName ??= UniqueName("First");
        lastName ??= UniqueName("Last");
        var response = await client.PostAsJsonAsync("/api/patients", new CreatePatientRequest
        {
            FirstName = firstName,
            LastName = lastName,
            DateOfBirth = dateOfBirth ?? DateOfBirth,
            Email = $"{Guid.NewGuid():N}@{PatientEmailDomain}",
        });
        return await response.ReadAsAsync<PatientResponse>(HttpStatusCode.Created);
    }

    protected static async Task<PatientProcedureStageDefinitionResponse> CreateStageDefinitionAsync(HttpClient admin, string? name = null)
    {
        name ??= UniqueName("Stage");
        var response = await admin.PostAsJsonAsync("/api/procedures/patient-procedure-stage/definitions",
            new CreatePatientProcedureStageDefinitionRequest
            {
                Name = name,
                Description = $"{name} description",
                Instructions = $"{name} instructions",
            });
        return await response.ReadAsAsync<PatientProcedureStageDefinitionResponse>(HttpStatusCode.Created);
    }

    protected static async Task<PatientProcedureStageTemplateResponse> CreateTemplateAsync(HttpClient admin, params Guid[] stageDefinitionIds)
    {
        var response = await admin.PostAsJsonAsync("/api/procedures/patient-procedure-stage/templates",
            new CreatePatientProcedureStageTemplateRequest
            {
                Name = UniqueName("Template"),
                StageDefinitionIdsInOrder = stageDefinitionIds,
            });
        return await response.ReadAsAsync<PatientProcedureStageTemplateResponse>(HttpStatusCode.Created);
    }

    /// <summary>A template of the given number of new stages.</summary>
    protected static async Task<PatientProcedureStageTemplateResponse> CreateTemplateWithStagesAsync(HttpClient admin, int stages)
    {
        var ids = new List<Guid>();
        for (var i = 0; i < stages; i++)
            ids.Add((await CreateStageDefinitionAsync(admin)).Id);
        return await CreateTemplateAsync(admin, [.. ids]);
    }

    /// <summary>Today in the UK, the day due dates are counted in.</summary>
    protected static DateOnly UkToday => DueDates.ToUkDate(DateTime.UtcNow);

    protected static async Task<PatientProcedureResponse> CreateProcedureAsync(
        HttpClient client,
        Guid patientId,
        Guid templateId,
        DateOnly? dueDate = null,
        string priority = "Medium")
    {
        var response = await client.PostAsJsonAsync("/api/procedures/patient-procedures", new CreatePatientProcedureRequest
        {
            PatientId = patientId,
            TemplateId = templateId,
            Title = UniqueName("Procedure"),
            Notes = string.Empty,
            DueDate = dueDate ?? UkToday.AddDays(7),
            Priority = priority,
        });
        return await response.ReadAsAsync<PatientProcedureResponse>(HttpStatusCode.Created);
    }

    /// <summary>A patient and a procedure for them from a new template of the given number of stages.</summary>
    protected static async Task<PatientProcedureResponse> CreatePatientWithProcedureAsync(HttpClient admin, int stages = 2)
    {
        var patient = await CreatePatientAsync(admin);
        var template = await CreateTemplateWithStagesAsync(admin, stages);
        return await CreateProcedureAsync(admin, patient.Id, template.Id);
    }

    protected static string ProcedureUrl(Guid procedureId) => $"/api/procedures/patient-procedures/{procedureId}";

    protected static string StageUrl(Guid procedureId, Guid stageId) => $"{ProcedureUrl(procedureId)}/stages/{stageId}";

    protected static Task<HttpResponseMessage> PutStageNotesAsync(HttpClient client, Guid procedureId, Guid stageId, string notes) =>
        client.PutAsJsonAsync($"{StageUrl(procedureId, stageId)}/notes", new UpdateProcedureStageNotesRequest { Notes = notes });

    protected static Task<HttpResponseMessage> FinishStageAsync(HttpClient client, Guid procedureId, Guid stageId) =>
        client.PostAsync($"{StageUrl(procedureId, stageId)}/finish", null);
}
