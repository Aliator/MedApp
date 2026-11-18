using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Appointments.Requests;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Contracts.Calendar.Requests;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Contracts.Common;
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

    /// <summary>The API's clock: the real time, until a test moves it (it's put back before each test).</summary>
    protected static TestClock Clock => Api.Clock;

    /// <summary>"Now" by the API's clock.</summary>
    protected static DateTime Now => Clock.GetUtcNow().UtcDateTime;

    /// <summary>Whether to make the first admin (and the Admin and User roles) before each test.</summary>
    protected virtual bool MakesFirstAdmin => true;

    [SetUp]
    public async Task ResetAsync()
    {
        Clock.Reset();
        await TestEnvironment.ResetDatabaseAsync();
        Api.Emails.Clear();

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
    /// for one of their own before anything else (every account an admin makes must), and signs them in again with it.
    /// With no roles the account can't change its password (that needs the Staff policy), so it keeps the generated one
    /// and stays held by the forced password change: give it a role, then remove it, to test the Staff policy itself.
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

    protected static async Task<PatientProcedureStageDefinitionResponse> CreateStageDefinitionAsync(
        HttpClient admin,
        string? name = null,
        string visitRequirement = "None")
    {
        name ??= UniqueName("Stage");
        var response = await admin.PostAsJsonAsync("/api/procedures/patient-procedure-stage/definitions",
            new CreatePatientProcedureStageDefinitionRequest
            {
                Name = name,
                Description = $"{name} description",
                Instructions = $"{name} instructions",
                VisitRequirement = visitRequirement,
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

    /// <summary>Today in the UK by the API's clock, the day due dates (and calendars) are counted in.</summary>
    protected static DateOnly UkToday => DueDates.ToUkDate(Now);

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

    /// <summary>A synthetic PDF, the smallest the API accepts as one.</summary>
    protected static readonly byte[] SyntheticPdf = System.Text.Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<< /Type /Catalog >>\nendobj\n%%EOF\n");

    /// <summary>
    /// Uploads a file as the web app does: its bytes as the body, its name percent-encoded in a header, and what it's
    /// attached to (<c>patientId=…</c>, <c>procedureId=…</c> or <c>stageId=…</c>) in the query.
    /// </summary>
    protected static Task<HttpResponseMessage> UploadAttachmentAsync(HttpClient client, string target, byte[] bytes, string fileName)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/attachments?{target}") { Content = content };
        request.Headers.Add("X-File-Name", Uri.EscapeDataString(fileName));
        return client.SendAsync(request);
    }

    protected static async Task<MedApp.Contracts.Attachments.Responses.AttachmentResponse> UploadAttachmentOkAsync(
        HttpClient client, string target, byte[] bytes, string fileName) =>
        await (await UploadAttachmentAsync(client, target, bytes, fileName))
            .ReadAsAsync<MedApp.Contracts.Attachments.Responses.AttachmentResponse>(HttpStatusCode.Created);

    protected static string ProcedureUrl(Guid procedureId) => $"/api/procedures/patient-procedures/{procedureId}";

    protected static string StageUrl(Guid procedureId, Guid stageId) => $"{ProcedureUrl(procedureId)}/stages/{stageId}";

    protected static Task<HttpResponseMessage> PutStageNotesAsync(HttpClient client, Guid procedureId, Guid stageId, string notes) =>
        client.PutAsJsonAsync($"{StageUrl(procedureId, stageId)}/notes", new UpdateProcedureStageNotesRequest { Notes = notes });

    /// <summary>Finishes a stage with a POST that has no body, as before stages could involve a visit.</summary>
    protected static Task<HttpResponseMessage> FinishStageAsync(HttpClient client, Guid procedureId, Guid stageId) =>
        client.PostAsync($"{StageUrl(procedureId, stageId)}/finish", null);

    /// <summary>Finishes a stage with a body: an admin finishing a Required stage without its visit, and why.</summary>
    protected static Task<HttpResponseMessage> FinishStageAsync(
        HttpClient client,
        Guid procedureId,
        Guid stageId,
        FinishProcedureStageRequest request) =>
        client.PostAsJsonAsync($"{StageUrl(procedureId, stageId)}/finish", request);

    protected static Task<HttpResponseMessage> PostProcedureStatusAsync(HttpClient client, Guid procedureId, string status, string? reason = null) =>
        client.PostAsJsonAsync($"{ProcedureUrl(procedureId)}/status", new ChangePatientProcedureStatusRequest { Status = status, Reason = reason });

    // Calendars and appointments.

    /// <summary>A UTC moment from a UK clock time on a UK date (GMT in winter, BST in summer).</summary>
    protected static DateTime UkAt(DateOnly date, int hour, int minute = 0) => UkTime.ToUtc(date, new TimeOnly(hour, minute));

    /// <summary>
    /// The first weekday (Monday to Friday) at least <paramref name="daysAhead"/> days after today by the API's clock,
    /// so a working-day time on it is in the future.
    /// </summary>
    protected static DateOnly NextWorkingDay(int daysAhead = 1)
    {
        var date = UkToday.AddDays(daysAhead);
        while (!WorkingHours.IsWorkingDay(date))
            date = date.AddDays(1);
        return date;
    }

    /// <summary>The next weekday at least a day ahead, at 09:00 UK time: a slot that's in the future and bookable.</summary>
    protected static DateTime NextBookableStart() => UkAt(NextWorkingDay(), 9);

    /// <summary>The first Monday at least a week after today (by the API's clock), so its whole week is ahead.</summary>
    protected static DateOnly MondayAWeekAhead()
    {
        var date = UkToday.AddDays(7);
        while (date.DayOfWeek != DayOfWeek.Monday)
            date = date.AddDays(1);
        return date;
    }

    /// <summary>Moves the API's clock to a minute after this moment (e.g. an appointment's start), so it has passed.</summary>
    protected static void MoveClockPast(DateTime utc) => Clock.SetUtcNow(utc.AddMinutes(1));

    protected static async Task<ActivityTypeResponse> CreateActivityTypeAsync(HttpClient admin, string? name = null, string colour = ActivityColour)
    {
        var response = await admin.PostAsJsonAsync("/api/calendar/activity-types",
            new CreateActivityTypeRequest { Name = name ?? UniqueName("Activity"), Colour = colour });
        return await response.ReadAsAsync<ActivityTypeResponse>(HttpStatusCode.Created);
    }

    protected static async Task<IReadOnlyList<ActivityTypeResponse>> GetActivityTypesAsync(HttpClient client, bool includeArchived = true) =>
        await (await client.GetAsync($"/api/calendar/activity-types?includeArchived={includeArchived}"))
            .ReadAsAsync<List<ActivityTypeResponse>>();

    /// <summary>A day template block at UK clock times, for the seeded Lunch by default.</summary>
    protected static DayTemplateBlockRequest TemplateBlock(int startHour, int endHour, Guid? activityTypeId = null, int startMinute = 0, int endMinute = 0) =>
        new()
        {
            ActivityTypeId = activityTypeId ?? LunchActivityTypeId,
            StartTime = new TimeOnly(startHour, startMinute),
            EndTime = new TimeOnly(endHour, endMinute),
        };

    /// <summary>Adds a day template to <paramref name="ownerUserId"/>'s own (as them, or as an admin).</summary>
    protected static async Task<DayTemplateResponse> CreateDayTemplateAsync(
        HttpClient client, Guid ownerUserId, params DayTemplateBlockRequest[] blocks)
    {
        var response = await PostDayTemplateAsync(client, ownerUserId, UniqueName("Day"), blocks);
        return await response.ReadAsAsync<DayTemplateResponse>(HttpStatusCode.Created);
    }

    protected static Task<HttpResponseMessage> PostDayTemplateAsync(
        HttpClient client, Guid ownerUserId, string name, params DayTemplateBlockRequest[] blocks) =>
        client.PostAsJsonAsync($"/api/calendar/staff/{ownerUserId}/day-templates",
            new DayTemplateRequest { Name = name, Blocks = [.. blocks] });

    protected static Task<HttpResponseMessage> UpdateDayTemplateAsync(HttpClient admin, DayTemplateResponse template, params DayTemplateBlockRequest[] blocks) =>
        admin.PutAsJsonAsync($"/api/calendar/day-templates/{template.Id}", new DayTemplateRequest { Name = template.Name, Blocks = [.. blocks] });

    /// <summary>
    /// A week template of <paramref name="ownerUserId"/>'s own using <paramref name="dayTemplateId"/> (or none) on every
    /// weekday.
    /// </summary>
    protected static async Task<WeekTemplateResponse> CreateWeekTemplateAsync(HttpClient client, Guid ownerUserId, Guid? dayTemplateId) =>
        await (await PostWeekTemplateAsync(client, ownerUserId, dayTemplateId)).ReadAsAsync<WeekTemplateResponse>(HttpStatusCode.Created);

    protected static Task<HttpResponseMessage> PostWeekTemplateAsync(HttpClient client, Guid ownerUserId, Guid? dayTemplateId) =>
        client.PostAsJsonAsync($"/api/calendar/staff/{ownerUserId}/week-templates", new WeekTemplateRequest
        {
            Name = UniqueName("Week"),
            Monday = dayTemplateId,
            Tuesday = dayTemplateId,
            Wednesday = dayTemplateId,
            Thursday = dayTemplateId,
            Friday = dayTemplateId,
        });

    protected static Task<HttpResponseMessage> SetDefaultWeekAsync(HttpClient client, Guid staffUserId, Guid? weekTemplateId) =>
        client.PutAsJsonAsync($"/api/calendar/staff/{staffUserId}/week-template", new SetStaffWeekTemplateRequest { WeekTemplateId = weekTemplateId });

    /// <summary>
    /// Gives a member of staff a default week of their own whose every weekday is a new day template of theirs with
    /// these blocks.
    /// </summary>
    protected static async Task<DayTemplateResponse> GiveDefaultWeekAsync(HttpClient admin, Guid staffUserId, params DayTemplateBlockRequest[] blocks)
    {
        var day = await CreateDayTemplateAsync(admin, staffUserId, blocks);
        var week = await CreateWeekTemplateAsync(admin, staffUserId, day.Id);
        await (await SetDefaultWeekAsync(admin, staffUserId, week.Id)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        return day;
    }

    protected static Task<HttpResponseMessage> PostBlockAsync(
        HttpClient client,
        Guid staffUserId,
        DateTime startUtc,
        DateTime endUtc,
        Guid? activityTypeId = null,
        string? note = null) =>
        client.PostAsJsonAsync("/api/calendar/blocks", new CreateCalendarBlockRequest
        {
            StaffUserId = staffUserId,
            ActivityTypeId = activityTypeId ?? TrainingActivityTypeId,
            StartUtc = startUtc,
            EndUtc = endUtc,
            Note = note,
        });

    protected static async Task<CalendarBlockResponse> CreateBlockAsync(
        HttpClient client,
        Guid staffUserId,
        DateTime startUtc,
        DateTime endUtc,
        Guid? activityTypeId = null) =>
        await (await PostBlockAsync(client, staffUserId, startUtc, endUtc, activityTypeId)).ReadAsAsync<CalendarBlockResponse>(HttpStatusCode.Created);

    protected static Task<HttpResponseMessage> PutBlockAsync(HttpClient client, Guid blockId, DateTime startUtc, DateTime endUtc, Guid? activityTypeId = null) =>
        client.PutAsJsonAsync($"/api/calendar/blocks/{blockId}", new UpdateCalendarBlockRequest
        {
            ActivityTypeId = activityTypeId ?? TrainingActivityTypeId,
            StartUtc = startUtc,
            EndUtc = endUtc,
        });

    protected static async Task<CalendarResponse> GetCalendarAsync(HttpClient client, Guid staffUserId, DateOnly from, DateOnly? to = null) =>
        await (await client.GetAsync($"/api/calendar?staffUserId={staffUserId}&from={from:yyyy-MM-dd}&to={to ?? from:yyyy-MM-dd}"))
            .ReadAsAsync<CalendarResponse>();

    protected static string AppointmentUrl(Guid appointmentId) => $"/api/appointments/{appointmentId}";

    /// <summary>Books a new appointment straight away (a member of staff, a start and an end): the response, whatever it is.</summary>
    protected static Task<HttpResponseMessage> PostBookingAsync(
        HttpClient client,
        Guid patientId,
        Guid staffUserId,
        DateTime startUtc,
        int minutes = AppointmentMinutes,
        Guid? procedureId = null,
        Guid? stageId = null,
        string? notes = null) =>
        client.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest
        {
            PatientId = patientId,
            ProcedureId = procedureId,
            StageId = stageId,
            StaffUserId = staffUserId,
            StartUtc = startUtc,
            EndUtc = startUtc.AddMinutes(minutes),
            Notes = notes,
        });

    protected static async Task<AppointmentResponse> BookNewAppointmentAsync(
        HttpClient client,
        Guid patientId,
        Guid staffUserId,
        DateTime startUtc,
        int minutes = AppointmentMinutes,
        Guid? procedureId = null,
        Guid? stageId = null) =>
        await (await PostBookingAsync(client, patientId, staffUserId, startUtc, minutes, procedureId, stageId))
            .ReadAsAsync<AppointmentResponse>(HttpStatusCode.Created);

    /// <summary>Adds an appointment to Requires Appointment (no member of staff or time yet).</summary>
    protected static async Task<AppointmentResponse> RequestAppointmentAsync(
        HttpClient client,
        Guid patientId,
        Guid? procedureId = null,
        Guid? stageId = null)
    {
        var response = await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest
        {
            PatientId = patientId,
            ProcedureId = procedureId,
            StageId = stageId,
        });
        return await response.ReadAsAsync<AppointmentResponse>(HttpStatusCode.Created);
    }

    /// <summary>Books a Requested appointment (as the caller saw it): the response, whatever it is.</summary>
    protected static Task<HttpResponseMessage> PostBookAsync(
        HttpClient client,
        Guid appointmentId,
        Guid staffUserId,
        DateTime startUtc,
        int minutes = AppointmentMinutes,
        string expectedStatus = "Requested") =>
        client.PostAsJsonAsync($"{AppointmentUrl(appointmentId)}/book", new BookAppointmentRequest
        {
            StaffUserId = staffUserId,
            StartUtc = startUtc,
            EndUtc = startUtc.AddMinutes(minutes),
            ExpectedStatus = expectedStatus,
        });

    /// <summary>Moves a Booked appointment (as the caller saw it) to another time or member of staff: the response, whatever it is.</summary>
    protected static Task<HttpResponseMessage> PostRescheduleAsync(
        HttpClient client, Guid appointmentId, Guid staffUserId, DateTime startUtc, int minutes = AppointmentMinutes) =>
        PostBookAsync(client, appointmentId, staffUserId, startUtc, minutes, "Booked");

    protected static Task<HttpResponseMessage> PostOutcomeAsync(HttpClient client, Guid appointmentId, string outcome) =>
        client.PostAsJsonAsync($"{AppointmentUrl(appointmentId)}/outcome", new RecordAppointmentOutcomeRequest { Outcome = outcome });

    protected static Task<HttpResponseMessage> PostCancelAsync(HttpClient client, Guid appointmentId, string reason) =>
        client.PostAsJsonAsync($"{AppointmentUrl(appointmentId)}/cancel", new CancelAppointmentRequest { Reason = reason });

    protected static async Task<AppointmentResponse> GetAppointmentAsync(HttpClient client, Guid appointmentId) =>
        await (await client.GetAsync(AppointmentUrl(appointmentId))).ReadAsAsync<AppointmentResponse>();

    /// <summary>Every appointment matching a query string (e.g. <c>procedureId=...</c>), up to a page of 100.</summary>
    protected static async Task<IReadOnlyList<AppointmentResponse>> SearchAppointmentsAsync(HttpClient client, string query) =>
        (await (await client.GetAsync($"/api/appointments/search?pageSize=100&{query}"))
            .ReadAsAsync<PagedResponse<AppointmentResponse>>()).Items;
}
