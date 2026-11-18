using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Jobs.Common;
using MedApp.Application.Monitoring.ServiceAlerts;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Monitoring.Requests;
using MedApp.Contracts.Monitoring.Responses;
using MedApp.Contracts.Notifications.Requests;
using MedApp.Contracts.Notifications.Responses;
using MedApp.Contracts.Patients.Responses;
using MedApp.Contracts.Pathways.PatientPathways.Requests;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Contracts.Pathways.PatientPathwayStages.Definitions.Responses;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MedApp.IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Monitoring;

/// <summary>
/// Admins' monitoring rules through the real pipeline: a staff member's change, saved and audited, fires the rule's
/// notification for its admin; conditions filter it; and only admins can manage rules, each only their own. Also each
/// person's switch for the "assigned to a pathway" notification.
/// </summary>
[TestFixture]
public sealed class MonitoringTests : IntegrationTest
{
    private const string RulesUrl = "/api/monitoring/rules";

    private HttpClient _admin = null!;
    private StaffAccount _staff = null!;
    private PatientPathwayStageDefinitionResponse _bloods = null!;
    private PatientPathwayStageDefinitionResponse _review = null!;
    private PathwayTemplateResponse _template = null!;
    private PatientResponse _patient = null!;

    [SetUp]
    public async Task CreateTemplateAsync()
    {
        _admin = await SignInAsAdminAsync();
        _staff = await CreateStaffAsync(UserRole);
        _bloods = await CreateStageDefinitionAsync(_admin);
        _review = await CreateStageDefinitionAsync(_admin);
        _template = await CreateTemplateAsync(_admin, _bloods.Id, _review.Id);
        _patient = await CreatePatientAsync(_staff.Client);
    }

    [Test]
    public async Task StaffFinishesTheWatchedStage_OnAHighPriorityPathway_NotifiesTheAdminWithoutFreeTextOrThePatient()
    {
        await CreateRuleAsync(_admin, new SaveMonitoringRuleRequest
        {
            SubjectUserId = _staff.UserId,
            Event = "StageFinished",
            MinimumPriority = "High",
            TemplateId = _template.Id,
            StageDefinitionId = _bloods.Id,
        });
        var pathway = await CreatePathwayAsync(_staff.Client, _patient.Id, _template.Id, priority: "High");

        await (await FinishStageAsync(_staff.Client, pathway.Id, pathway.Stages[0].Id)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        var notification = (await NotificationsOfAsync(_admin)).Items.Should().ContainSingle().Subject;
        notification.Should().BeEquivalentTo(new
        {
            Kind = "MonitoredStageFinished",
            Title = "Stage finished",
            PathwayId = (Guid?)pathway.Id,
            IsRead = false,
        });
        notification.Message.Should().Be(
            $"{_staff.Username} finished the {_bloods.Name} stage on a High priority {_template.Name} pathway.");
        foreach (var text in new[] { notification.Title, notification.Message })
            text.Should().NotContain(pathway.Title).And.NotContain(_patient.FirstName).And.NotContain(_patient.LastName);
        // The staff member isn't told anything.
        (await NotificationsOfAsync(_staff.Client)).Items.Should().BeEmpty();
    }

    [Test]
    public async Task Conditions_FilterTheEvents()
    {
        await CreateRuleAsync(_admin, new SaveMonitoringRuleRequest
        {
            Event = "StageFinished",
            MinimumPriority = "High",
            StageDefinitionId = _bloods.Id,
        });

        // Too low a priority: nothing.
        var medium = await CreatePathwayAsync(_staff.Client, _patient.Id, _template.Id, priority: "Medium");
        await (await FinishStageAsync(_staff.Client, medium.Id, medium.Stages[0].Id)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        // The other stage: nothing.
        var urgent = await CreatePathwayAsync(_staff.Client, _patient.Id, _template.Id, priority: "Urgent");
        await (await FinishStageAsync(_staff.Client, urgent.Id, urgent.Stages[0].Id)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        var afterBloods = await NotificationsOfAsync(_admin);
        await (await FinishStageAsync(_staff.Client, urgent.Id, urgent.Stages[1].Id)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        afterBloods.Items.Should().ContainSingle().Which.PathwayId.Should().Be(urgent.Id);
        (await NotificationsOfAsync(_admin)).Items.Should().ContainSingle("finishing the second stage matches no rule");
    }

    [Test]
    public async Task SwitchedOffRule_SendsNothing_UntilSwitchedOn()
    {
        var rule = await CreateRuleAsync(_admin, new SaveMonitoringRuleRequest { Event = "PathwayCreated", IsEnabled = false });

        await CreatePathwayAsync(_staff.Client, _patient.Id, _template.Id);
        (await NotificationsOfAsync(_admin)).Items.Should().BeEmpty();

        await (await _admin.PutAsJsonAsync($"{RulesUrl}/{rule.Id}", new SaveMonitoringRuleRequest { Event = "PathwayCreated", IsEnabled = true }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        await CreatePathwayAsync(_staff.Client, _patient.Id, _template.Id);

        var notification = (await NotificationsOfAsync(_admin)).Items.Should().ContainSingle().Subject;
        notification.Kind.Should().Be("MonitoredPathwayCreated");
        notification.Message.Should().Be($"{_staff.Username} created a Medium priority {_template.Name} pathway.");
    }

    [Test]
    public async Task AdminsOwnChange_DoesNotNotifyThem_ButNotifiesAnotherAdminWatching()
    {
        var other = await CreateStaffAsync(AdminRole, UserRole);
        await CreateRuleAsync(_admin, new SaveMonitoringRuleRequest { Event = "PathwayBlocked" });
        await CreateRuleAsync(other.Client, new SaveMonitoringRuleRequest { Event = "PathwayBlocked" });
        await CreateRuleAsync(other.Client, new SaveMonitoringRuleRequest { Event = "PathwayBlocked", MinimumPriority = "Low" });
        var pathway = await CreatePathwayAsync(_admin, _patient.Id, _template.Id);

        await (await _admin.PostAsJsonAsync($"{PathwayUrl(pathway.Id)}/status",
                new ChangePatientPathwayStatusRequest { Status = "Blocked", Reason = BlockReason }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        (await NotificationsOfAsync(_admin)).Items.Should().BeEmpty();
        var told = (await NotificationsOfAsync(other.Client)).Items.Should().ContainSingle("two matching rules of one admin make one notification").Subject;
        told.Message.Should().Be($"{AdminUsername} put a Medium priority {_template.Name} pathway on hold.").And.NotContain(BlockReason);
    }

    [Test]
    public async Task OwnerNoLongerAnAdmin_IsNoLongerTold()
    {
        var other = await CreateStaffAsync(AdminRole, UserRole);
        await CreateRuleAsync(other.Client, new SaveMonitoringRuleRequest { Event = "PathwayCreated" });
        (await _admin.PutAsJsonAsync($"/api/auth/users/{other.Username}/roles", new SetUserRolesRequest([UserRole])))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        await CreatePathwayAsync(_staff.Client, _patient.Id, _template.Id);

        (await NotificationsOfAsync(other.Client)).Items.Should().BeEmpty();
    }

    [Test]
    public async Task AssignedRule_IsAboutWhoWasAssigned()
    {
        var other = await CreateStaffAsync(AdminRole, UserRole);
        await CreateRuleAsync(other.Client, new SaveMonitoringRuleRequest { SubjectUserId = _staff.UserId, Event = "PathwayAssigned" });
        var pathway = await CreatePathwayAsync(_admin, _patient.Id, _template.Id);

        await (await _admin.PostAsync($"{PathwayUrl(pathway.Id)}/assignments/{_staff.UserId}", null)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        (await NotificationsOfAsync(other.Client)).Items.Should().ContainSingle()
            .Which.Message.Should().Be($"{AdminUsername} assigned {_staff.Username} to a Medium priority {_template.Name} pathway.");
    }

    [Test]
    public async Task AdminUnlocksAWatchedStage_NotifiesAnotherAdmin()
    {
        var other = await CreateStaffAsync(AdminRole, UserRole);
        var adminId = await GetUserIdAsync(_admin, AdminUsername);
        await CreateRuleAsync(other.Client, new SaveMonitoringRuleRequest { SubjectUserId = adminId, Event = "StageUnlocked", StageDefinitionId = _bloods.Id });
        var pathway = await CreatePathwayAsync(_staff.Client, _patient.Id, _template.Id);
        await (await FinishStageAsync(_staff.Client, pathway.Id, pathway.Stages[0].Id)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        await (await _admin.PostAsync($"{StageUrl(pathway.Id, pathway.Stages[0].Id)}/unlock", null)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        var told = (await NotificationsOfAsync(other.Client)).Items.Should().ContainSingle().Subject;
        told.Kind.Should().Be("MonitoredStageUnlocked");
        told.Message.Should().Be($"{AdminUsername} unlocked the {_bloods.Name} stage on a Medium priority {_template.Name} pathway.");
    }

    /// <summary>
    /// The bypass is monitored (decision 136): a senior clinician finishes a Required stage without its visit, and an
    /// admin watching for that is told, once, without the reason given.
    /// </summary>
    [Test]
    public async Task SeniorClinicianFinishesARequiredStageWithoutItsVisit_NotifiesTheAdminWatchingForIt()
    {
        var senior = await CreateStaffAsync(SeniorClinicianRole);
        var visit = await CreateStageDefinitionAsync(_admin, visitRequirement: "Required");
        var template = await CreateTemplateAsync(_admin, visit.Id, _review.Id);
        await CreateRuleAsync(_admin, new SaveMonitoringRuleRequest { Event = "StageFinishedWithoutVisit", StageDefinitionId = visit.Id });
        await CreateRuleAsync(_admin, new SaveMonitoringRuleRequest { Event = "StageFinished" });
        var pathway = await CreatePathwayAsync(senior.Client, _patient.Id, template.Id);

        await (await FinishStageAsync(senior.Client, pathway.Id, pathway.Stages[0].Id,
                new FinishPathwayStageRequest { WithoutVisit = true, Reason = WithoutVisitReason }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        var told = (await NotificationsOfAsync(_admin)).Items.Should().ContainSingle().Subject;
        told.Kind.Should().Be("MonitoredStageFinishedWithoutVisit");
        told.Title.Should().Be("Stage finished without visit");
        told.Message.Should().Be($"{senior.Username} finished the {visit.Name} stage on a Medium priority {template.Name} pathway without its required visit.");
        told.Message.Should().NotContain(WithoutVisitReason);
    }

    [Test]
    public async Task DueDateMovedWhileThePriorityIsLowered_StillMatchesAnUrgentRule()
    {
        await CreateRuleAsync(_admin, new SaveMonitoringRuleRequest { Event = "DueDateChanged", MinimumPriority = "Urgent" });
        var pathway = await CreatePathwayAsync(_staff.Client, _patient.Id, _template.Id, priority: "Urgent");
        var from = pathway.DueDate;
        var to = from.AddDays(30);

        await (await _staff.Client.PatchAsJsonAsync(PathwayUrl(pathway.Id), new UpdatePatientPathwayRequest { DueDate = to, Priority = "Low" }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        var told = (await NotificationsOfAsync(_admin)).Items.Should().ContainSingle().Subject;
        told.Kind.Should().Be("MonitoredDueDateChanged");
        told.Message.Should().StartWith($"{_staff.Username} moved the target date of a Low priority {_template.Name} pathway from ");
    }

    [Test]
    public async Task RuleNamingADeletedAccount_CanStillBeSwitchedOffAndEdited()
    {
        var leaver = await CreateStaffAsync(UserRole);
        var rule = await CreateRuleAsync(_admin, new SaveMonitoringRuleRequest
        {
            SubjectUserId = leaver.UserId, Event = "StageFinished", StageDefinitionId = _bloods.Id, TemplateId = _template.Id,
        });
        await (await _admin.DeleteAsJsonAsync($"/api/auth/users/{leaver.Username}", new DeleteUserRequest(AdminPassword)))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        var switched = await (await _admin.PutAsJsonAsync($"{RulesUrl}/{rule.Id}/enabled", new SetMonitoringRuleEnabledRequest { IsEnabled = false }))
            .ReadAsAsync<MonitoringRuleResponse>();
        var edited = await (await _admin.PutAsJsonAsync($"{RulesUrl}/{rule.Id}", new SaveMonitoringRuleRequest
            {
                SubjectUserId = leaver.UserId, Event = "StageFinished", StageDefinitionId = _bloods.Id, TemplateId = _template.Id,
                MinimumPriority = "High", IsEnabled = false,
            }))
            .ReadAsAsync<MonitoringRuleResponse>();

        switched.Should().BeEquivalentTo(new { IsEnabled = false, SubjectUserId = (Guid?)leaver.UserId, SubjectUsername = (string?)null });
        edited.MinimumPriority.Should().Be("High");
        // Naming a deleted account afresh is still refused.
        await (await _admin.PutAsJsonAsync($"{RulesUrl}/{rule.Id}", new SaveMonitoringRuleRequest { SubjectUserId = Guid.NewGuid(), Event = "StageFinished" }))
            .ShouldHaveStatusAsync(HttpStatusCode.NotFound);
        await (await _admin.PutAsJsonAsync($"{RulesUrl}/{Guid.NewGuid()}/enabled", new SetMonitoringRuleEnabledRequest { IsEnabled = true }))
            .ShouldHaveStatusAsync(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task StageUnlockedRule_ForSomeoneNotAnAdmin_Returns400()
    {
        await (await _admin.PostAsJsonAsync(RulesUrl, new SaveMonitoringRuleRequest { SubjectUserId = _staff.UserId, Event = "StageUnlocked" }))
            .ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task AdminWatchingAssignments_IsAssigned_GetsOnlyTheAssignmentNotification()
    {
        var other = await CreateStaffAsync(AdminRole, UserRole);
        await CreateRuleAsync(other.Client, new SaveMonitoringRuleRequest { Event = "PathwayAssigned" });
        var pathway = await CreatePathwayAsync(_admin, _patient.Id, _template.Id);

        await (await _admin.PostAsync($"{PathwayUrl(pathway.Id)}/assignments/{other.UserId}", null)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        (await NotificationsOfAsync(other.Client)).Items.Should().ContainSingle().Which.Kind.Should().Be("PathwayAssigned");
    }

    [Test]
    public async Task Rules_AreEachAdminsOwn()
    {
        var other = await CreateStaffAsync(AdminRole, UserRole);
        var mine = await CreateRuleAsync(_admin, new SaveMonitoringRuleRequest
        {
            SubjectUserId = _staff.UserId, Event = "StageFinished", StageDefinitionId = _review.Id, TemplateId = _template.Id,
        });

        var listed = await (await _admin.GetAsync(RulesUrl)).ReadAsAsync<List<MonitoringRuleResponse>>();
        listed.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            mine.Id, SubjectUsername = _staff.Username, Event = "StageFinished", StageName = _review.Name, TemplateName = _template.Name,
        });
        (await (await other.Client.GetAsync(RulesUrl)).ReadAsAsync<List<MonitoringRuleResponse>>()).Should().BeEmpty();
        await (await other.Client.PutAsJsonAsync($"{RulesUrl}/{mine.Id}", new SaveMonitoringRuleRequest { Event = "PathwayCreated" }))
            .ShouldHaveStatusAsync(HttpStatusCode.NotFound);
        await (await other.Client.DeleteAsync($"{RulesUrl}/{mine.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NotFound);

        await (await _admin.DeleteAsync($"{RulesUrl}/{mine.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        (await (await _admin.GetAsync(RulesUrl)).ReadAsAsync<List<MonitoringRuleResponse>>()).Should().BeEmpty();
    }

    [Test]
    public async Task NonAdmin_CannotManageRules()
    {
        await (await _staff.Client.GetAsync(RulesUrl)).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await (await _staff.Client.PostAsJsonAsync(RulesUrl, new SaveMonitoringRuleRequest { Event = "PathwayCreated" }))
            .ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await (await _staff.Client.PutAsJsonAsync($"{RulesUrl}/{Guid.NewGuid()}", new SaveMonitoringRuleRequest { Event = "PathwayCreated" }))
            .ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await (await _staff.Client.DeleteAsync($"{RulesUrl}/{Guid.NewGuid()}")).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task CreateRule_Invalid_Returns400_AndUnknownStage_Returns404()
    {
        await (await _admin.PostAsJsonAsync(RulesUrl, new SaveMonitoringRuleRequest { Event = "PathwayCreated", StageDefinitionId = _bloods.Id }))
            .ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
        await (await _admin.PostAsJsonAsync(RulesUrl, new SaveMonitoringRuleRequest { Event = "Teleported" }))
            .ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
        await (await _admin.PostAsJsonAsync(RulesUrl, new SaveMonitoringRuleRequest { Event = "StageFinished", StageDefinitionId = Guid.NewGuid() }))
            .ShouldHaveStatusAsync(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task AssignedNotification_TurnedOff_IsNotSent_AndTurnedOnAgain_Is()
    {
        (await (await _staff.Client.GetAsync("/api/notifications/preferences")).ReadAsAsync<NotificationPreferencesResponse>())
            .PathwayAssigned.Should().BeTrue();
        (await (await _staff.Client.PutAsJsonAsync("/api/notifications/preferences", new UpdateNotificationPreferencesRequest { PathwayAssigned = false }))
            .ReadAsAsync<NotificationPreferencesResponse>()).PathwayAssigned.Should().BeFalse();
        var first = await CreatePathwayAsync(_admin, _patient.Id, _template.Id);

        await (await _admin.PostAsync($"{PathwayUrl(first.Id)}/assignments/{_staff.UserId}", null)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        (await NotificationsOfAsync(_staff.Client)).Items.Should().BeEmpty();
        (await (await _staff.Client.GetAsync("/api/notifications/preferences")).ReadAsAsync<NotificationPreferencesResponse>())
            .PathwayAssigned.Should().BeFalse();

        await (await _staff.Client.PutAsJsonAsync("/api/notifications/preferences", new UpdateNotificationPreferencesRequest { PathwayAssigned = true }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        var second = await CreatePathwayAsync(_admin, _patient.Id, _template.Id);
        await (await _admin.PostAsync($"{PathwayUrl(second.Id)}/assignments/{_staff.UserId}", null)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        (await NotificationsOfAsync(_staff.Client)).Items.Should().ContainSingle().Which.PathwayId.Should().Be(second.Id);
    }

    [Test]
    public async Task ServiceAlerts_EveryEventWithEveryConditionItTakes_RunsAsSql_AndAWaitingVisitIsReportedOnce()
    {
        foreach (var monitoringEvent in new[] { "ReferralNotTriaged", "ResultNotSignedOff", "VisitNotBooked", "SpecimenOverdue", "WaitBreached", "PauseReviewPassed" })
        {
            await CreateRuleAsync(_admin, new SaveMonitoringRuleRequest
            {
                SubjectUserId = monitoringEvent == "ResultNotSignedOff" ? _staff.UserId : null,
                Event = monitoringEvent,
                MinimumPriority = "Medium",
                TemplateId = monitoringEvent == "ReferralNotTriaged" ? null : _template.Id,
                StageDefinitionId = monitoringEvent is "VisitNotBooked" or "SpecimenOverdue" ? _bloods.Id : null,
                ThresholdDays = 0,
                SpecialtyId = _template.SpecialtyId,
            });
        }

        // A template condition for every alert but triage translates; nothing waits yet.
        (await RunServiceAlertsAsync(DateTime.UtcNow)).Should().Be(0);

        // A Requested visit with no conditions, waiting since before "now": reported once, with a count.
        var pathway = await CreatePathwayAsync(_staff.Client, _patient.Id, _template.Id, priority: "High");
        await ExecuteSqlAsync($"""
            INSERT INTO [Appointments] ([Id], [PatientId], [PathwayId], [Status], [Notes], [IsAutomatic], [CreatedAtUtc], [CreatedByUserId], [IsRecordedAfterEvent], [LastUpdated])
            VALUES ({Guid.NewGuid()}, {_patient.Id}, {pathway.Id}, 0, '', 0, {DateTime.UtcNow.AddDays(-3)}, {_staff.UserId}, 0, {DateTime.UtcNow})
            """);
        await CreateRuleAsync(_admin, new SaveMonitoringRuleRequest { Event = "VisitNotBooked", ThresholdDays = 2 });

        (await RunServiceAlertsAsync(DateTime.UtcNow)).Should().Be(1);
        (await RunServiceAlertsAsync(DateTime.UtcNow)).Should().Be(0);
        (await NotificationsOfAsync(_admin)).Items.Should().ContainSingle(n => n.Kind == "MonitoredVisitNotBooked")
            .Which.Message.Should().Be("1 visit has waited more than 2 days to be booked.");
    }

    [Test]
    public async Task ServiceAlert_DaysOnAChange_OrNoneOnAnAlert_IsRefused()
    {
        (await _admin.PostAsJsonAsync(RulesUrl, new SaveMonitoringRuleRequest { Event = "PathwayCreated", ThresholdDays = 2 }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _admin.PostAsJsonAsync(RulesUrl, new SaveMonitoringRuleRequest { Event = "SpecimenOverdue" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static Task<int> RunServiceAlertsAsync(DateTime nowUtc) =>
        RunInScopeAsync(services => services.GetServices<IScheduledJob>()
            .Single(job => job.Name == ServiceAlertsJob.JobName)
            .RunAsync(nowUtc, CancellationToken.None));

    private static async Task<MonitoringRuleResponse> CreateRuleAsync(HttpClient admin, SaveMonitoringRuleRequest request) =>
        await (await admin.PostAsJsonAsync(RulesUrl, request)).ReadAsAsync<MonitoringRuleResponse>(HttpStatusCode.Created);

    private static async Task<NotificationsResponse> NotificationsOfAsync(HttpClient client) =>
        await (await client.GetAsync("/api/notifications")).ReadAsAsync<NotificationsResponse>();
}
