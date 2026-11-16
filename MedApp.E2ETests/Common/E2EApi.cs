using System.Text.Json;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Contracts.Notifications.Responses;
using MedApp.Contracts.Patients.Responses;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Responses;
using MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Responses;
using Microsoft.Playwright;

namespace MedApp.E2ETests.Common;

/// <summary>
/// The app's API, signed in as the E2E account (or another account a test made), for what a test needs around the
/// journey it checks: making its data first, checking what the UI did, and deleting it all afterwards. Everything a
/// test makes is synthetic, with a unique name, and registered with <see cref="E2EData"/> to be deleted.
/// </summary>
public sealed class E2EApi(IAPIRequestContext request)
{
    private const string Stages = "/api/procedures/patient-procedure-stage";
    private const string Procedures = "/api/procedures/patient-procedures";

    public IAPIRequestContext Request => request;

    public async Task<PatientResponse> CreatePatientAsync(string firstName, string lastName, DateOnly dateOfBirth) =>
        await SendAsync<PatientResponse>("POST", "/api/patients", new
        {
            firstName,
            lastName,
            dateOfBirth,
            email = $"{lastName.ToLowerInvariant().Replace(' ', '.')}@example.test",
        });

    public Task<PatientResponse?> FindPatientAsync(Guid id) => FindAsync<PatientResponse>($"/api/patients/{id}");

    public async Task<IReadOnlyList<PatientResponse>> SearchPatientsByLastNameAsync(string lastName) =>
        (await SendAsync<PagedResponse<PatientResponse>>("GET", $"/api/patients/search?lastName={Uri.EscapeDataString(lastName)}&pageSize=100")).Items;

    public Task DeletePatientAsync(Guid id) => DeleteIfThereAsync($"/api/patients/{id}");

    /// <summary>A stage definition; <paramref name="visitRequirement"/> is None, Suggested or Required.</summary>
    public async Task<PatientProcedureStageDefinitionResponse> CreateStageDefinitionAsync(
        string name, string instructions, string visitRequirement = "None") =>
        await SendAsync<PatientProcedureStageDefinitionResponse>("POST", $"{Stages}/definitions", new
        {
            name,
            description = $"{name}: a synthetic stage for the end-to-end tests.",
            instructions,
            visitRequirement,
        });

    public Task DeleteStageDefinitionAsync(Guid id) => DeleteIfThereAsync($"{Stages}/definitions/{id}");

    public async Task<PatientProcedureStageTemplateResponse> CreateTemplateAsync(string name, params Guid[] stageDefinitionIds) =>
        await SendAsync<PatientProcedureStageTemplateResponse>("POST", $"{Stages}/templates", new
        {
            name,
            stageDefinitionIdsInOrder = stageDefinitionIds,
        });

    public Task DeleteTemplateAsync(Guid id) => DeleteIfThereAsync($"{Stages}/templates/{id}");

    public async Task<PatientProcedureResponse> CreateProcedureAsync(Guid patientId, Guid templateId, string title, DateOnly dueDate) =>
        await SendAsync<PatientProcedureResponse>("POST", Procedures, new
        {
            patientId,
            templateId,
            title,
            notes = string.Empty,
            dueDate,
            priority = "Medium",
        });

    public Task<PatientProcedureResponse?> FindProcedureAsync(Guid id) => FindAsync<PatientProcedureResponse>($"{Procedures}/{id}");

    public async Task<IReadOnlyList<PatientProcedureSummaryResponse>> ProceduresOfPatientAsync(Guid patientId) =>
        (await SendAsync<PagedResponse<PatientProcedureSummaryResponse>>(
            "GET", $"{Procedures}/search?patientId={patientId}&includeClosed=true&pageSize=100")).Items;

    public async Task AssignAsync(Guid procedureId, Guid userId) =>
        await SendAsync<PatientProcedureResponse>("POST", $"{Procedures}/{procedureId}/assignments/{userId}");

    public async Task SaveStageNotesAsync(Guid procedureId, Guid stageId, string notes) =>
        await SendAsync<PatientProcedureResponse>("PUT", $"{Procedures}/{procedureId}/stages/{stageId}/notes", new { notes });

    /// <summary>Cancels a procedure if it's still open, then deletes it (only cancelled ones can be). Needs an admin.</summary>
    public async Task CancelAndDeleteProcedureAsync(Guid id)
    {
        var procedure = await FindProcedureAsync(id);
        if (procedure is null)
            return;
        if (procedure.AllowedStatusChanges.Contains("Cancelled"))
            await SendAsync<PatientProcedureResponse>("POST", $"{Procedures}/{id}/status", new { status = "Cancelled", reason = "End-to-end test clean-up." });
        await DeleteIfThereAsync($"{Procedures}/{id}");
    }

    /// <summary>All of a patient's appointments, whatever their status.</summary>
    public async Task<IReadOnlyList<AppointmentResponse>> AppointmentsOfPatientAsync(Guid patientId) =>
        (await SendAsync<PagedResponse<AppointmentResponse>>("GET", $"/api/appointments/search?patientId={patientId}&pageSize=100")).Items;

    /// <summary>Cancels an appointment (the app never deletes them). Only Requested and Booked ones can be.</summary>
    public async Task CancelAppointmentAsync(Guid id, string reason) =>
        await SendAsync<AppointmentResponse>("POST", $"/api/appointments/{id}/cancel", new { reason });

    /// <summary>One person's calendar for one day: its blocks (stored and from templates) and appointments.</summary>
    public async Task<CalendarResponse> CalendarAsync(Guid staffUserId, DateOnly date) =>
        await SendAsync<CalendarResponse>("GET", $"/api/calendar?staffUserId={staffUserId}&from={date:yyyy-MM-dd}&to={date:yyyy-MM-dd}");

    /// <summary>Deletes a stored block, if it's still there.</summary>
    public Task DeleteBlockAsync(Guid id) => DeleteIfThereAsync($"/api/calendar/blocks/{id}");

    public async Task<WhoAmIResponse> WhoAmIAsync() => await SendAsync<WhoAmIResponse>("GET", "/api/auth/whoami");

    public async Task<Guid> UserIdOfAsync(string username) =>
        (await SendAsync<UserResponse>("GET", $"/api/auth/users/{Uri.EscapeDataString(username)}")).UserId;

    public Task<UserResponse?> FindUserAsync(string username) =>
        FindAsync<UserResponse>($"/api/auth/users/{Uri.EscapeDataString(username)}");

    /// <summary>Makes an account; returns its generated first password.</summary>
    public async Task<string> CreateUserAsync(string username, params string[] roles)
    {
        var created = await SendAsync<CreateUserResponse>("POST", "/api/auth/users", new { username });
        if (roles.Length > 0)
            await SendAsync<object>("PUT", $"/api/auth/users/{Uri.EscapeDataString(username)}/roles", new { roles });
        return created.Password;
    }

    /// <summary>Changes the signed-in account's own password (a new account must, before anything else).</summary>
    public async Task UpdateOwnPasswordAsync(string username, string oldPassword, string newPassword) =>
        await SendAsync<object>("PUT", $"/api/auth/users/{Uri.EscapeDataString(username)}/update-password", new { oldPassword, newPassword });

    /// <summary>Deletes an account, confirmed with the signed-in admin's password. Nothing happens if it's gone already.</summary>
    public async Task DeleteUserAsync(string username, string adminPassword)
    {
        var response = await request.DeleteAsync($"/api/auth/users/{Uri.EscapeDataString(username)}",
            new APIRequestContextOptions { DataObject = new { password = adminPassword } });
        if (!response.Ok && response.Status != 404)
            throw new InvalidOperationException($"Deleting user {username} failed with {response.Status}: {await response.TextAsync()}");
    }

    public async Task<NotificationsResponse> NotificationsAsync() => await SendAsync<NotificationsResponse>("GET", "/api/notifications");

    public async Task MarkNotificationReadAsync(Guid id) => await SendAsync<object>("POST", $"/api/notifications/{id}/read");

    private async Task<T> SendAsync<T>(string method, string url, object? body = null)
    {
        var response = await request.FetchAsync(url, new APIRequestContextOptions { Method = method, DataObject = body });
        if (!response.Ok)
            throw new InvalidOperationException($"{method} {url} failed with {response.Status}: {await response.TextAsync()}");
        var text = await response.TextAsync();
        return string.IsNullOrEmpty(text) ? default! : JsonSerializer.Deserialize<T>(text, JsonSerializerOptions.Web)!;
    }

    private async Task<T?> FindAsync<T>(string url) where T : class
    {
        var response = await request.GetAsync(url);
        if (response.Status == 404)
            return null;
        if (!response.Ok)
            throw new InvalidOperationException($"GET {url} failed with {response.Status}: {await response.TextAsync()}");
        return JsonSerializer.Deserialize<T>(await response.TextAsync(), JsonSerializerOptions.Web);
    }

    private async Task DeleteIfThereAsync(string url)
    {
        var response = await request.DeleteAsync(url);
        if (!response.Ok && response.Status != 404)
            throw new InvalidOperationException($"DELETE {url} failed with {response.Status}: {await response.TextAsync()}");
    }

    private sealed record PagedResponse<T>(IReadOnlyList<T> Items, int TotalCount);
}
