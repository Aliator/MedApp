using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Insights.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.Insights.Responses;
using MedApp.Contracts.Procedures.PatientProcedures.Requests;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.Infrastructure.Data;
using MedApp.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Procedures;

/// <summary>
/// Insights on SQL Server: the same LINQ runs in SQL here as in the unit tests' memory, so each total is checked
/// against the in-memory answer on the same rows, as well as against what the procedures' history says it should be.
/// </summary>
[TestFixture]
public sealed class InsightsTests : IntegrationTest
{
    private HttpClient _admin = null!;
    private StaffAccount _staff = null!;

    [SetUp]
    public async Task SignInAsync()
    {
        _admin = await SignInAsAdminAsync();
        _staff = await CreateStaffAsync(UserRole);
    }

    [Test]
    public async Task GetInsights_Staff_IsForbidden()
    {
        var response = await _staff.Client.GetAsync("/api/procedures/insights");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task GetInsights_Admin_CountsTheWorkAndChecksTheTemplate()
    {
        var template = await CreateTemplateWithStagesAsync(_admin, 2);
        var patient = await CreatePatientAsync(_admin);
        var assigned = await CreateProcedureAsync(_admin, patient.Id, template.Id, priority: "Urgent");
        await AssignAsync(assigned.Id, _staff.UserId);
        var overdue = await CreateProcedureAsync(_admin, patient.Id, template.Id, priority: "Urgent");
        await MakeOverdueAsync(overdue.Id, UkToday.AddDays(-3));

        var insights = await (await _admin.GetAsync($"/api/procedures/insights?Weeks=4&TemplateId={template.Id}"))
            .ReadAsAsync<ProcedureInsightsResponse>();

        insights.Weeks.Should().Be(4);
        insights.TemplateId.Should().Be(template.Id);
        insights.Open.Current.Should().Be(2);
        insights.Overdue.Current.Should().Be(1);
        insights.OverdueByAssignee.Should().ContainSingle(a => a.UserId == null && a.Count == 1);
        insights.OverdueByPriority.Single(p => p.Priority == "Urgent").Count.Should().Be(1);
        insights.Throughput[^1].Created.Should().Be(2);
        (await _admin.GetAsync($"/api/procedures/insights?TemplateId={Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _admin.GetAsync("/api/procedures/insights?Weeks=0")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task StatusCountsAt_HistorySinceTheMoment_RebuildsTheStateThenInSqlAsInMemory()
    {
        var template = await CreateTemplateWithStagesAsync(_admin, 2);
        var patient = await CreatePatientAsync(_admin);

        var assigned = await CreateProcedureAsync(_admin, patient.Id, template.Id);
        await AssignAsync(assigned.Id, _staff.UserId);
        var lateThen = await CreateProcedureAsync(_admin, patient.Id, template.Id);
        await MakeOverdueAsync(lateThen.Id, UkToday.AddDays(-2));
        var completed = await CreateProcedureAsync(_admin, patient.Id, template.Id);
        await AssignAsync(completed.Id, _staff.UserId);
        foreach (var stage in completed.Stages)
            (await FinishStageAsync(_admin, completed.Id, stage.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        await ChangeStatusAsync(completed.Id, "Completed");

        await Task.Delay(50);
        var moment = DateTime.UtcNow;
        await Task.Delay(50);

        // Since the moment: one blocked, one's due date moved on, one reopened.
        await ChangeStatusAsync(assigned.Id, "Blocked", "Waiting for results.");
        (await _admin.PatchAsJsonAsync(ProcedureUrl(lateThen.Id), new UpdatePatientProcedureRequest { DueDate = UkToday.AddDays(20) }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await _admin.PostAsync($"{StageUrl(completed.Id, completed.Stages[1].Id)}/unlock", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var later = await CreateProcedureAsync(_admin, patient.Id, template.Id);

        var inSql = await RunInScopeAsync(services =>
            services.GetRequiredService<IProcedureInsightsRepository>().GetStatusCountsAtAsync(null, moment, CancellationToken.None));
        var inMemory = await InMemoryAsync((procedures, audit, _) => procedures.StatusCountsAt(audit, moment).ToList());

        inSql.Should().BeEquivalentTo(inMemory);
        inSql.Should().BeEquivalentTo([
            new StatusCount(PatientProcedureStatus.Assigned, false, 1),
            new StatusCount(PatientProcedureStatus.Unassigned, true, 1),
            new StatusCount(PatientProcedureStatus.Completed, false, 1),
        ]);
        later.CreatedAt.Should().BeAfter(moment);
    }

    [Test]
    public async Task StageDurations_FinishedStages_AreAveragedInSqlAsInMemory()
    {
        var template = await CreateTemplateWithStagesAsync(_admin, 2);
        var patient = await CreatePatientAsync(_admin);
        for (var i = 0; i < 3; i++)
        {
            var procedure = await CreateProcedureAsync(_admin, patient.Id, template.Id);
            (await PutStageNotesAsync(_admin, procedure.Id, procedure.Stages[0].Id, "Started.")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await FinishStageAsync(_admin, procedure.Id, procedure.Stages[0].Id)).StatusCode.Should().Be(HttpStatusCode.OK);
            if (i == 0)
                (await FinishStageAsync(_admin, procedure.Id, procedure.Stages[1].Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var (from, to) = (DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddMinutes(1));
        var inSql = await RunInScopeAsync(services =>
            services.GetRequiredService<IProcedureInsightsRepository>().GetStageDurationsAsync(template.Id, from, to, CancellationToken.None));
        var inMemory = await InMemoryAsync((procedures, audit, stages) =>
            procedures.Where(p => p.TemplateId == template.Id).StageDurations(stages, audit, from, to).ToList());

        inSql.Select(r => (r.StageOrder, r.FinishedCount)).Should().BeEquivalentTo([(1, 3), (2, 1)]);
        inSql.Select(r => (r.StageOrder, r.FinishedCount, r.TemplateName)).Should()
            .BeEquivalentTo(inMemory.Select(r => (r.StageOrder, r.FinishedCount, r.TemplateName)));
        // SQL counts second boundaries crossed, C# whole seconds elapsed: they can differ by a second.
        foreach (var row in inSql)
        {
            var memory = inMemory.Single(r => r.StageOrder == row.StageOrder);
            row.AverageWaitingSeconds.Should().BeApproximately(memory.AverageWaitingSeconds, 1);
            row.AverageWorkingSeconds.Should().BeApproximately(memory.AverageWorkingSeconds, 1);
        }
    }

    [Test]
    public async Task CompletionsAndCreations_ByHour_CountInSqlAsInMemory()
    {
        var template = await CreateTemplateWithStagesAsync(_admin, 1);
        var patient = await CreatePatientAsync(_admin);
        var procedure = await CreateProcedureAsync(_admin, patient.Id, template.Id);
        (await FinishStageAsync(_admin, procedure.Id, procedure.Stages[0].Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        await ChangeStatusAsync(procedure.Id, "Completed");
        await CreateProcedureAsync(_admin, patient.Id, template.Id);

        var (from, to) = (DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddMinutes(1));
        var (created, completed) = await RunInScopeAsync(async services =>
        {
            var repository = services.GetRequiredService<IProcedureInsightsRepository>();
            return (await repository.CountCreationsByHourAsync(template.Id, from, to, CancellationToken.None),
                await repository.CountCompletionsByHourAsync(template.Id, from, to, CancellationToken.None));
        });

        created.Sum(h => h.Count).Should().Be(2);
        completed.Sum(h => h.Count).Should().Be(1);
        created.Should().AllSatisfy(h => h.StartUtc.Should().BeOnOrBefore(DateTime.UtcNow));
    }

    private async Task AssignAsync(Guid procedureId, Guid userId) =>
        (await _admin.PostAsync($"{ProcedureUrl(procedureId)}/assignments/{userId}", null)).StatusCode.Should().Be(HttpStatusCode.OK);

    private async Task ChangeStatusAsync(Guid procedureId, string status, string? reason = null) =>
        (await _admin.PostAsJsonAsync($"{ProcedureUrl(procedureId)}/status", new ChangePatientProcedureStatusRequest { Status = status, Reason = reason }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

    /// <summary>Due dates can't be set in the past through the API, so this moves one there in the database.</summary>
    private static Task<int> MakeOverdueAsync(Guid procedureId, DateOnly dueDate) =>
        ExecuteSqlAsync($"UPDATE PatientProcedures SET DueDateUtc = {DueDates.EndOfDayUtc(dueDate)} WHERE Id = {procedureId}");

    /// <summary>Runs a query over every row, loaded into memory, as the unit tests run it.</summary>
    private static Task<T> InMemoryAsync<T>(
        Func<IQueryable<PatientProcedure>, IQueryable<PatientProcedureAuditEntry>, IQueryable<PatientProcedureStage>, T> query) =>
        RunInScopeAsync(async services =>
        {
            var context = services.GetRequiredService<MedAppDbContext>();
            var procedures = await context.PatientProcedures.AsNoTracking().ToListAsync();
            var audit = await context.PatientProcedureAuditEntries.AsNoTracking().ToListAsync();
            var stages = await context.PatientProcedureStages.AsNoTracking().ToListAsync();
            return query(procedures.AsQueryable(), audit.AsQueryable(), stages.AsQueryable());
        });
}
