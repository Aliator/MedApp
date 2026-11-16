using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Common;
using MedApp.Contracts.Procedures.PatientProcedures.Requests;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Procedures;

/// <summary>Due dates: whole UK days, stored as the end of the day in UTC, and searched by day.</summary>
[TestFixture]
public sealed class DueDateTests : IntegrationTest
{
    private HttpClient _admin = null!;
    private Guid _patientId;
    private Guid _templateId;

    [SetUp]
    public async Task CreateTemplateAsync()
    {
        _admin = await SignInAsAdminAsync();
        _patientId = (await CreatePatientAsync(_admin)).Id;
        _templateId = (await CreateTemplateWithStagesAsync(_admin, 1)).Id;
    }

    [Test]
    public async Task CreateProcedure_SummerDueDate_IsDueAtTheEndOfThatDayBritishSummerTime()
    {
        var date = NextDate(month: 7, day: 15);

        var procedure = await CreateProcedureAsync(_admin, _patientId, _templateId, date);

        procedure.DueDate.Should().Be(date);
        procedure.DueDateUtc.Should().Be(date.ToDateTime(new TimeOnly(22, 59, 59)).AddTicks(TimeSpan.TicksPerSecond - 1));
        (await GetAsync(procedure.Id)).DueDateUtc.Should().Be(DueDates.EndOfDayUtc(date));
    }

    [Test]
    public async Task CreateProcedure_WinterDueDate_IsDueAtTheEndOfThatDayGreenwichMeanTime()
    {
        var date = NextDate(month: 1, day: 15);

        var procedure = await CreateProcedureAsync(_admin, _patientId, _templateId, date);

        procedure.DueDate.Should().Be(date);
        (await GetAsync(procedure.Id)).DueDateUtc.Should().Be(date.ToDateTime(TimeOnly.MaxValue));
    }

    [Test]
    public async Task CreateProcedure_DueToday_IsAccepted()
    {
        var procedure = await CreateProcedureAsync(_admin, _patientId, _templateId, UkToday);

        procedure.DueDate.Should().Be(UkToday);
        procedure.IsOverdue.Should().BeFalse();
    }

    [Test]
    public async Task CreateProcedure_DueYesterday_Returns400ValidationProblem()
    {
        var response = await _admin.PostAsJsonAsync("/api/procedures/patient-procedures", new CreatePatientProcedureRequest
        {
            PatientId = _patientId,
            TemplateId = _templateId,
            DueDate = UkToday.AddDays(-1),
            Priority = "Low",
        });

        var problem = await response.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainKey("DueDate");
    }

    [Test]
    public async Task SearchProcedures_DueDateRange_MatchesWholeUkDays()
    {
        var inRange = await CreateProcedureAsync(_admin, _patientId, _templateId, UkToday.AddDays(3));
        await CreateProcedureAsync(_admin, _patientId, _templateId, UkToday.AddDays(2));
        await CreateProcedureAsync(_admin, _patientId, _templateId, UkToday.AddDays(4));
        var day = UkToday.AddDays(3).ToString("yyyy-MM-dd");

        var page = await SearchAsync($"dueFrom={day}&dueTo={day}");

        page.Items.Should().ContainSingle().Which.Id.Should().Be(inRange.Id);
    }

    [Test]
    public async Task SearchProcedures_OverdueOnly_ReturnsOpenProceduresPastTheirDueDate()
    {
        var overdue = await CreateProcedureAsync(_admin, _patientId, _templateId);
        await CreateProcedureAsync(_admin, _patientId, _templateId);
        // Due dates can't be set in the past, so move one back as time passing would.
        await ExecuteSqlAsync($"UPDATE PatientProcedures SET DueDateUtc = {DateTime.UtcNow.AddDays(-1)} WHERE Id = {overdue.Id}");

        var page = await SearchAsync("overdueOnly=true");

        page.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(new { overdue.Id, IsOverdue = true });
        (await GetAsync(overdue.Id)).IsOverdue.Should().BeTrue();
    }

    [Test]
    public async Task SearchProcedures_ByDefault_SortsBySoonestDueAndLeavesOutClosedOnes()
    {
        var later = await CreateProcedureAsync(_admin, _patientId, _templateId, UkToday.AddDays(9));
        var sooner = await CreateProcedureAsync(_admin, _patientId, _templateId, UkToday.AddDays(1));
        var cancelled = await CreateProcedureAsync(_admin, _patientId, _templateId, UkToday);
        await (await _admin.PostAsJsonAsync($"{ProcedureUrl(cancelled.Id)}/status",
            new ChangePatientProcedureStatusRequest { Status = "Cancelled", Reason = BlockReason })).ShouldHaveStatusAsync(HttpStatusCode.OK);

        var open = await SearchAsync("");
        var all = await SearchAsync("includeClosed=true");

        open.Items.Select(p => p.Id).Should().Equal(sooner.Id, later.Id);
        all.Items.Select(p => p.Id).Should().Equal(cancelled.Id, sooner.Id, later.Id);
    }

    /// <summary>The next time that day comes round, at least a day from now (due dates can't be in the past).</summary>
    private static DateOnly NextDate(int month, int day)
    {
        var date = new DateOnly(UkToday.Year, month, day);
        return date > UkToday ? date : date.AddYears(1);
    }

    private async Task<PatientProcedureResponse> GetAsync(Guid id) =>
        await (await _admin.GetAsync(ProcedureUrl(id))).ReadAsAsync<PatientProcedureResponse>();

    private async Task<PagedResponse<PatientProcedureSummaryResponse>> SearchAsync(string query) =>
        await (await _admin.GetAsync($"/api/procedures/patient-procedures/search?{query}"))
            .ReadAsAsync<PagedResponse<PatientProcedureSummaryResponse>>();
}
