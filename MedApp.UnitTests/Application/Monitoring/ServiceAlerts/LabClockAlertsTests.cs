using FluentAssertions;
using MedApp.Application.Monitoring.Commands.CreateMonitoringRule;
using MedApp.Application.Monitoring.ServiceAlerts;
using MedApp.Domain.Appointments;
using MedApp.Domain.Lab;
using MedApp.Domain.Monitoring;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Referrals;
using MedApp.Domain.Results;
using MedApp.Domain.Specimens;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.MonitoringTestConstants;

namespace MedApp.UnitTests.Application.Monitoring.ServiceAlerts;

/// <summary>MC19's lab clock alerts: the rule's checks, the cases narrowed in SQL, the clock in memory, and the words.</summary>
[TestFixture]
public sealed class LabClockAlertsTests
{
    // Noon on 10 June 2026; a case received on 1 June (Histology: due 8 June, at risk from 6 June).
    private static readonly DateTime Now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 6, 10);
    private static readonly Guid Clinic = Guid.Parse("0000000c-0000-0000-0000-0000000000c1");

    private static LabCase Case(DateTime receivedAtUtc, LabCaseStatus status = LabCaseStatus.Open, Guid? clinic = null) => new()
    {
        Id = Guid.NewGuid(),
        Status = status,
        ReceivedAtUtc = receivedAtUtc,
        RequestingTeamId = clinic ?? Clinic,
        LabTest = SpecimensTestConstants.Histology(),
    };

    private static MonitoringRule Rule(MonitoringEvent monitoringEvent, int days, Guid? teamId = null) => new()
    {
        Id = Guid.NewGuid(),
        OwnerUserId = OwnerId,
        Event = monitoringEvent,
        ThresholdDays = days,
        TeamId = teamId,
        IsEnabled = true,
    };

    private static List<Guid> Find(MonitoringRule rule, IEnumerable<LabCase> cases, IEnumerable<MonitoringAlertSent>? sent = null)
    {
        var sources = new ServiceAlertSources(
            Array.Empty<Referral>().AsQueryable(), Array.Empty<ReferralClockPause>().AsQueryable(), Array.Empty<Result>().AsQueryable(),
            Array.Empty<Appointment>().AsQueryable(), Array.Empty<Specimen>().AsQueryable(), Array.Empty<PatientPathwayStage>().AsQueryable(),
            (sent ?? []).AsQueryable(), cases.AsQueryable());
        return ServiceAlertCandidates.LabCasesToCheck(sources, rule).ToList()
            .Where(c => ServiceAlertCandidates.IsLabCaseDue(c, rule, Now, Today))
            .Select(c => c.Id)
            .ToList();
    }

    [Test]
    public void Overdue_IsPastTheDueDateByMoreThanTheDays_OpenOnly()
    {
        var breached = Case(new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc));
        var onTime = Case(Now.AddDays(-1));
        var reported = Case(new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc), LabCaseStatus.Reported);
        var notReceived = new LabCase { Id = Guid.NewGuid(), Status = LabCaseStatus.Open };

        Find(Rule(MonitoringEvent.LabCaseOverdue, 0), [breached, onTime, reported, notReceived]).Should().Equal(breached.Id);
        Find(Rule(MonitoringEvent.LabCaseOverdue, 1), [breached]).Should().Equal(breached.Id);
        Find(Rule(MonitoringEvent.LabCaseOverdue, 2), [breached]).Should().BeEmpty("10 June is only 2 days past 8 June");
    }

    [Test]
    public void AtRisk_HasBeenAtRiskMoreThanTheDays_ForTheRulesClinic_OncePerRule()
    {
        var atRisk = Case(new DateTime(2026, 6, 5, 9, 0, 0, DateTimeKind.Utc));
        var elsewhere = Case(new DateTime(2026, 6, 5, 9, 0, 0, DateTimeKind.Utc), clinic: Guid.NewGuid());
        var rule = Rule(MonitoringEvent.LabCaseAtRisk, 0, Clinic);

        Find(rule, [atRisk, elsewhere]).Should().Equal(atRisk.Id);
        Find(rule, [atRisk], [new MonitoringAlertSent { RuleId = rule.Id, SubjectId = atRisk.Id, SentAtUtc = Now }]).Should().BeEmpty();
        Find(Rule(MonitoringEvent.LabCaseAtRisk, 2), [atRisk]).Should().BeEmpty("it's been at risk since 10 June only");
    }

    [TestCase(MonitoringEvent.LabCaseAtRisk, 1, 0, "1 lab case is at risk of missing its due date.")]
    [TestCase(MonitoringEvent.LabCaseAtRisk, 2, 3, "2 lab cases have been at risk of missing their due date for more than 3 days.")]
    [TestCase(MonitoringEvent.LabCaseOverdue, 1, 0, "1 lab case is past its due date.")]
    [TestCase(MonitoringEvent.LabCaseOverdue, 3, 1, "3 lab cases are more than 1 day past their due date.")]
    public void Message_SaysHowManyAndHowLate(MonitoringEvent monitoringEvent, int count, int days, string expected)
    {
        ServiceAlertNotifications.MessageOf(monitoringEvent, count, days, null, null).Should().Be(expected);
        ServiceAlertNotifications.TitleOf(monitoringEvent).Should().StartWith("Lab cases");
    }

    [TestCase("LabCaseAtRisk")]
    [TestCase("LabCaseOverdue")]
    public void Rule_RefusesATemplateOrAPriority_ButTakesATeam(string monitoringEvent)
    {
        var validator = new CreateMonitoringRuleValidator();
        var rule = new CreateMonitoringRuleCommand(OwnerId, null, monitoringEvent, null, null, null, true, 2, Clinic);

        validator.Validate(rule).IsValid.Should().BeTrue();
        validator.Validate(rule with { TemplateId = TemplateId }).Errors
            .Should().ContainSingle(e => e.ErrorMessage == "A lab case has no template.");
        validator.Validate(rule with { MinimumPriority = "High" }).Errors
            .Should().ContainSingle(e => e.ErrorMessage == "A lab case has no pathway priority.");
    }
}
