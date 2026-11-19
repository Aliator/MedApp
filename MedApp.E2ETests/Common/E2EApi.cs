using System.Text.Json;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Contracts.Letters.Responses;
using MedApp.Contracts.Notepads.Requests;
using MedApp.Contracts.Notepads.Responses;
using MedApp.Contracts.Notifications.Responses;
using MedApp.Contracts.Patients.Responses;
using MedApp.Contracts.Referrals.Responses;
using MedApp.Contracts.Roles.Responses;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Contracts.Pathways.PatientPathwayStages.Definitions.Responses;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MedApp.Contracts.Teams.Responses;
using Microsoft.Playwright;

namespace MedApp.E2ETests.Common;

/// <summary>
/// The app's API, signed in as the E2E admin, the run's member of staff (<see cref="E2EStaff"/>) or another account a
/// test made, for what a test needs around the
/// journey it checks: making its data first, checking what the UI did, and deleting it all afterwards. Everything a
/// test makes is synthetic, with a unique name, and registered with <see cref="E2EData"/> to be deleted.
/// </summary>
public sealed class E2EApi(IAPIRequestContext request)
{
    private const string Stages = "/api/pathways/patient-pathway-stage";
    private const string Pathways = "/api/pathways/patient-pathways";

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
    public async Task<PatientPathwayStageDefinitionResponse> CreateStageDefinitionAsync(
        string name, string instructions, string visitRequirement = "None") =>
        await SendAsync<PatientPathwayStageDefinitionResponse>("POST", $"{Stages}/definitions", new
        {
            name,
            description = $"{name}: a synthetic stage for the end-to-end tests.",
            instructions,
            visitRequirement,
        });

    public Task DeleteStageDefinitionAsync(Guid id) => DeleteIfThereAsync($"{Stages}/definitions/{id}");

    /// <summary>Deletes a stage definition, or archives it if a template or pathway that stays still uses it.</summary>
    public Task DeleteOrArchiveStageDefinitionAsync(Guid id) => DeleteOrArchiveAsync($"{Stages}/definitions/{id}");

    /// <summary>A template in a team (the E2E clinic: <see cref="E2EStaff.Team"/>). Needs an admin.</summary>
    public async Task<PathwayTemplateResponse> CreateTemplateAsync(string name, Guid teamId, params Guid[] stageDefinitionIds) =>
        await SendAsync<PathwayTemplateResponse>("POST", $"{Stages}/templates", new
        {
            name,
            teamId,
            stageDefinitionIdsInOrder = stageDefinitionIds,
        });

    public Task DeleteTemplateAsync(Guid id) => DeleteIfThereAsync($"{Stages}/templates/{id}");

    /// <summary>Deletes a template, or archives it if a pathway that stays was made from it.</summary>
    public Task DeleteOrArchiveTemplateAsync(Guid id) => DeleteOrArchiveAsync($"{Stages}/templates/{id}");

    public async Task<PatientPathwayResponse> CreatePathwayAsync(Guid patientId, Guid templateId, string title, DateOnly dueDate) =>
        await SendAsync<PatientPathwayResponse>("POST", Pathways, new
        {
            patientId,
            templateId,
            title,
            notes = string.Empty,
            dueDate,
            priority = "Medium",
        });

    public Task<PatientPathwayResponse?> FindPathwayAsync(Guid id) => FindAsync<PatientPathwayResponse>($"{Pathways}/{id}");

    public async Task<IReadOnlyList<PatientPathwaySummaryResponse>> PathwaysOfPatientAsync(Guid patientId) =>
        (await SendAsync<PagedResponse<PatientPathwaySummaryResponse>>(
            "GET", $"{Pathways}/search?patientId={patientId}&includeClosed=true&pageSize=100")).Items;

    public async Task AssignAsync(Guid pathwayId, Guid userId) =>
        await SendAsync<PatientPathwayResponse>("POST", $"{Pathways}/{pathwayId}/assignments/{userId}");

    public async Task SaveStageNotesAsync(Guid pathwayId, Guid stageId, string notes) =>
        await SendAsync<PatientPathwayResponse>("PUT", $"{Pathways}/{pathwayId}/stages/{stageId}/notes", new { notes });

    /// <summary>
    /// Cancels a pathway if it's still open, then deletes it (only cancelled ones can be). Needs a member of its team with
    /// DeletePathways (the run's member of staff).
    /// </summary>
    public async Task CancelAndDeletePathwayAsync(Guid id)
    {
        var pathway = await FindPathwayAsync(id);
        if (pathway is null)
            return;
        if (pathway.AllowedStatusChanges.Contains("Cancelled"))
            await SendAsync<PatientPathwayResponse>("POST", $"{Pathways}/{id}/status", new { status = "Cancelled", reason = "End-to-end test clean-up." });
        await DeleteIfThereAsync($"{Pathways}/{id}");
    }

    /// <summary>All of a patient's appointments, whatever their status.</summary>
    public async Task<IReadOnlyList<AppointmentResponse>> AppointmentsOfPatientAsync(Guid patientId) =>
        (await SendAsync<PagedResponse<AppointmentResponse>>("GET", $"/api/appointments/search?patientId={patientId}&pageSize=100")).Items;

    /// <summary>Cancels an appointment (the app never deletes them). Only Requested and Booked ones can be.</summary>
    public async Task CancelAppointmentAsync(Guid id, string reason) =>
        await SendAsync<AppointmentResponse>("POST", $"/api/appointments/{id}/cancel", new { reason });

    /// <summary>One person's calendar for one day: its blocks (stored and from their templates), appointments and meetings.</summary>
    public Task<CalendarResponse> CalendarAsync(Guid staffUserId, DateOnly date) => CalendarAsync(staffUserId, date, date);

    /// <summary>One person's calendar from one UK day to another (at most 31): its blocks, appointments and meetings.</summary>
    public async Task<CalendarResponse> CalendarAsync(Guid staffUserId, DateOnly from, DateOnly to) =>
        await SendAsync<CalendarResponse>("GET", $"/api/calendar?staffUserId={staffUserId}&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

    /// <summary>Cancels (deletes) a meeting, as an admin. Nothing happens if it's gone already.</summary>
    public Task CancelMeetingAsync(Guid id) => DeleteIfThereAsync($"/api/calendar/meetings/{id}");

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

    /// <summary>The signed-in account's notepad.</summary>
    public async Task<NotepadResponse> NotepadAsync() => await SendAsync<NotepadResponse>("GET", "/api/me/notepad");

    /// <summary>Saves the signed-in account's notepad from the version it has now.</summary>
    public async Task<NotepadResponse> SaveNotepadAsync(string text)
    {
        var current = await NotepadAsync();
        return await SendAsync<NotepadResponse>("PUT", "/api/me/notepad", new SaveNotepadRequest { Text = text, ExpectedVersion = current.Version });
    }

    /// <summary>The teams a member of staff belongs to, exactly, each as a member. Needs an admin.</summary>
    public async Task SetTeamsAsync(string username, params Guid[] teamIds) =>
        await SendAsync<object>("PUT", $"/api/auth/users/{Uri.EscapeDataString(username)}/teams", new
        {
            teams = teamIds.Select(teamId => new { teamId, role = "Member" }).ToArray(),
        });

    /// <summary>
    /// The clinic team with this name: found (restored if archived) or made. Never deleted: what the journeys keep (a
    /// referral, a letter's pathway) stays in it (README). Needs an admin.
    /// </summary>
    public async Task<TeamResponse> EnsureTeamAsync(string name)
    {
        var team = (await SendAsync<List<TeamResponse>>("GET", "/api/teams?includeArchived=true"))
            .FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
        if (team is null)
            return await SendAsync<TeamResponse>("POST", "/api/teams", new { name });
        if (team.IsArchived)
            return await SendAsync<TeamResponse>("POST", $"/api/teams/{team.Id}/restore", new { expectedVersion = team.Version });
        return team;
    }

    /// <summary>A role granting these permissions, by name. Needs an admin.</summary>
    public async Task<RoleResponse> CreateRoleAsync(string name, IReadOnlyList<string> permissions) =>
        await SendAsync<RoleResponse>("POST", "/api/roles", new
        {
            name,
            description = "Made by the end-to-end tests for their run's member of staff; deleted when the run ends.",
            permissions,
        });

    /// <summary>Deletes a role nobody holds any more. Nothing happens if it's gone already. Needs an admin.</summary>
    public Task DeleteRoleAsync(Guid id) => DeleteIfThereAsync($"/api/roles/{id}");

    /// <summary>
    /// Registers a referral for a patient, received two days ago, asking for Urgent. Referrals are never deleted, and
    /// a patient with one can't be: both stay (README).
    /// </summary>
    public async Task<ReferralResponse> RegisterReferralAsync(Guid patientId, Guid teamId, string reason) =>
        await SendAsync<ReferralResponse>("POST", "/api/referrals", new
        {
            patientId,
            teamId,
            receivedOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)).ToString("yyyy-MM-dd"),
            referrerPractice = "Synthetic Lane Surgery (E2E)",
            requestedPriority = "Urgent",
            reason,
            confirmPossibleDuplicate = true,
        });

    public Task<ReferralResponse?> FindReferralAsync(Guid id) => FindAsync<ReferralResponse>($"/api/referrals/{id}");

    /// <summary>
    /// A letter template for every team: a clinic letter whose closing names the approver. Templates a letter was
    /// written from can't be deleted, only archived (<see cref="ArchiveLetterTemplateAsync"/>). Needs an admin.
    /// </summary>
    public async Task<LetterTemplateResponse> CreateLetterTemplateAsync(string name) =>
        await SendAsync<LetterTemplateResponse>("POST", "/api/letter-templates", new
        {
            name,
            kind = "ClinicLetter",
            letterhead = "Synthetic Specialist Centre (E2E)",
            bodySkeleton = "Dear Doctor,\n\nRe: {{patient.name}}, born {{patient.dateOfBirth}}",
            closing = "Yours sincerely,\n\n{{approver.name}}",
        });

    /// <summary>Archives a letter template (it can't be chosen again), or deletes it if no letter was written from it.</summary>
    public async Task ArchiveOrDeleteLetterTemplateAsync(Guid id)
    {
        var template = await FindAsync<LetterTemplateResponse>($"/api/letter-templates/{id}");
        if (template is null)
            return;
        if (template.CanBeDeleted)
            await DeleteIfThereAsync($"/api/letter-templates/{id}");
        else if (!template.IsArchived)
            await SendAsync<LetterTemplateResponse>("POST", $"/api/letter-templates/{id}/archive", new { expectedVersion = template.Version });
    }

    /// <summary>A member of staff's full name and job title, as letters print them. Needs an admin.</summary>
    public async Task SetStaffDetailsAsync(string username, string fullName, string jobTitle) =>
        await SendAsync<object>("PUT", $"/api/auth/users/{Uri.EscapeDataString(username)}/details", new { fullName, jobTitle });

    public Task<LetterResponse?> FindLetterAsync(Guid id) => FindAsync<LetterResponse>($"/api/letters/{id}");

    /// <summary>
    /// Cancels a pathway if it's still open. A pathway with letters can't be deleted (they're clinical correspondence):
    /// it stays, cancelled (README). Needs a member of its team (the run's member of staff).
    /// </summary>
    public async Task CancelPathwayAsync(Guid id)
    {
        var pathway = await FindPathwayAsync(id);
        if (pathway is not null && pathway.AllowedStatusChanges.Contains("Cancelled"))
            await SendAsync<PatientPathwayResponse>("POST", $"{Pathways}/{id}/status", new { status = "Cancelled", reason = "End-to-end test clean-up." });
    }

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

    /// <summary>Deletes, or archives what the API won't delete because something that stays uses it (a 409).</summary>
    private async Task DeleteOrArchiveAsync(string url)
    {
        var response = await request.DeleteAsync(url);
        if (response.Ok || response.Status == 404)
            return;
        if (response.Status != 409)
            throw new InvalidOperationException($"DELETE {url} failed with {response.Status}: {await response.TextAsync()}");
        var archived = await request.PostAsync($"{url}/archive");
        if (!archived.Ok)
            throw new InvalidOperationException($"POST {url}/archive failed with {archived.Status}: {await archived.TextAsync()}");
    }

    private sealed record PagedResponse<T>(IReadOnlyList<T> Items, int TotalCount);
}
