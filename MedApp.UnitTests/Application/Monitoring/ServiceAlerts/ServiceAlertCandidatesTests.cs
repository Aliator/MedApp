using FluentAssertions;
using MedApp.Application.Monitoring.ServiceAlerts;
using MedApp.Domain.Appointments;
using MedApp.Domain.Monitoring;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Referrals;
using MedApp.Domain.Results;
using MedApp.Domain.Specimens;
using static MedApp.UnitTests.Common.Constants.MonitoringTestConstants;

namespace MedApp.UnitTests.Application.Monitoring.ServiceAlerts;

[TestFixture]
public sealed class ServiceAlertCandidatesTests
{
    private static readonly DateTime Now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 6, 10);
    private static readonly Guid OtherSpecialtyId = Guid.Parse("0000000c-0000-0000-0000-0000000000f1");

    private List<Referral> _referrals = null!;
    private List<ReferralClockPause> _pauses = null!;
    private List<Result> _results = null!;
    private List<Appointment> _appointments = null!;
    private List<Specimen> _specimens = null!;
    private List<PatientPathwayStage> _stages = null!;
    private List<MonitoringAlertSent> _sent = null!;

    [SetUp]
    public void SetUp()
    {
        _referrals = [];
        _pauses = [];
        _results = [];
        _appointments = [];
        _specimens = [];
        _stages = [];
        _sent = [];
    }

    private IReadOnlyList<Guid> Find(MonitoringRule rule) =>
        ServiceAlertCandidates.Find(
            new ServiceAlertSources(
                _referrals.AsQueryable(), _pauses.AsQueryable(), _results.AsQueryable(), _appointments.AsQueryable(),
                _specimens.AsQueryable(), _stages.AsQueryable(), _sent.AsQueryable()),
            rule, Now, Today).ToList();

    private static PatientPathway Pathway(Guid? specialtyId = null, PatientPathwayPriority priority = PatientPathwayPriority.Medium) => new()
    {
        Id = Guid.NewGuid(),
        SpecialtyId = specialtyId ?? RuleSpecialtyId,
        Priority = priority,
        TemplateId = TemplateId,
    };

    private Referral AddReferral(
        ReferralStatus status = ReferralStatus.AwaitingTriage,
        int receivedDaysAgo = 3,
        Guid? specialtyId = null,
        DateOnly? targetOn = null,
        DateOnly? pausedSince = null,
        DateOnly? stoppedOn = null)
    {
        var referral = new Referral
        {
            Id = Guid.NewGuid(),
            Status = status,
            SpecialtyId = specialtyId ?? RuleSpecialtyId,
            ReceivedOn = Today.AddDays(-receivedDaysAgo),
            RequestedPriority = PatientPathwayPriority.Medium,
            TargetOn = targetOn ?? Today.AddDays(30),
            PausedSinceOn = pausedSince,
            StoppedOn = stoppedOn,
        };
        _referrals.Add(referral);
        return referral;
    }

    [Test]
    public void ReferralNotTriaged_AwaitingLongerThanTheDays_IsReported()
    {
        var old = AddReferral(receivedDaysAgo: 3);
        AddReferral(receivedDaysAgo: 2);
        AddReferral(status: ReferralStatus.InformationRequested, receivedDaysAgo: 10);
        AddReferral(status: ReferralStatus.Accepted, receivedDaysAgo: 10);
        AddReferral(receivedDaysAgo: 10, specialtyId: OtherSpecialtyId);

        var rule = CreateRule(MonitoringEvent.ReferralNotTriaged, thresholdDays: 2, specialtyId: RuleSpecialtyId);

        Find(rule).Should().Equal(old.Id);
    }

    [Test]
    public void Find_ItemAlreadyReportedUnderTheRule_IsLeftOut_ButNotUnderAnother()
    {
        var referral = AddReferral(receivedDaysAgo: 5);
        var rule = CreateRule(MonitoringEvent.ReferralNotTriaged, thresholdDays: 1);
        _sent.Add(new MonitoringAlertSent { RuleId = rule.Id, SubjectId = referral.Id, SentAtUtc = Now });

        Find(rule).Should().BeEmpty();
        Find(CreateRule(MonitoringEvent.ReferralNotTriaged, thresholdDays: 1)).Should().Equal(referral.Id);
    }

    [Test]
    public void ResultNotSignedOff_ReportedLongerAgo_ForTheResponsibleClinician()
    {
        var pathway = Pathway();
        var waiting = new Result
        {
            Id = Guid.NewGuid(), Status = ResultStatus.Seen, ReportedAtUtc = Now.AddDays(-3), ResponsibleUserId = OwnerId,
            PathwayId = pathway.Id, Pathway = pathway
        };
        _results.Add(waiting);
        _results.Add(new Result { Id = Guid.NewGuid(), Status = ResultStatus.SignedOff, ReportedAtUtc = Now.AddDays(-9), ResponsibleUserId = OwnerId });
        _results.Add(new Result { Id = Guid.NewGuid(), Status = ResultStatus.AwaitingSignOff, ReportedAtUtc = Now.AddDays(-1), ResponsibleUserId = OwnerId });
        _results.Add(new Result { Id = Guid.NewGuid(), Status = ResultStatus.AwaitingSignOff, ReportedAtUtc = Now.AddDays(-9), ResponsibleUserId = OtherOwnerId });

        var rule = CreateRule(MonitoringEvent.ResultNotSignedOff, subjectUserId: OwnerId, thresholdDays: 2, specialtyId: RuleSpecialtyId);

        Find(rule).Should().Equal(waiting.Id);
    }

    [Test]
    public void VisitNotBooked_RequestedLongerAgo_OnThatStage()
    {
        var pathway = Pathway();
        var stage = new PatientPathwayStage { Id = Guid.NewGuid(), StageDefinitionId = Guid.NewGuid() };
        var waiting = new Appointment
        {
            Id = Guid.NewGuid(), Status = AppointmentStatus.Requested, CreatedAtUtc = Now.AddDays(-4), Pathway = pathway, Stage = stage
        };
        _appointments.Add(waiting);
        _appointments.Add(new Appointment { Id = Guid.NewGuid(), Status = AppointmentStatus.Booked, CreatedAtUtc = Now.AddDays(-9), Pathway = pathway, Stage = stage });
        _appointments.Add(new Appointment { Id = Guid.NewGuid(), Status = AppointmentStatus.Requested, CreatedAtUtc = Now.AddDays(-9), Pathway = pathway });

        var rule = CreateRule(MonitoringEvent.VisitNotBooked, thresholdDays: 3, stageDefinitionId: stage.StageDefinitionId);

        Find(rule).Should().Equal(waiting.Id);
    }

    [Test]
    public void SpecimenOverdue_MoreThanTheDaysPastItsTurnaround_WhileOpen()
    {
        var pathway = Pathway(priority: PatientPathwayPriority.Urgent);
        var type = new SpecimenType { Id = Guid.NewGuid(), ExpectedTurnaroundDays = 5 };
        Specimen Add(SpecimenStatus status, int takenDaysAgo)
        {
            var specimen = new Specimen
            {
                Id = Guid.NewGuid(), Status = status, TakenAtUtc = Now.AddDays(-takenDaysAgo), SpecimenType = type, Pathway = pathway
            };
            _specimens.Add(specimen);
            return specimen;
        }

        var overdue = Add(SpecimenStatus.Sent, 8);
        Add(SpecimenStatus.Sent, 6);
        Add(SpecimenStatus.Reported, 20);

        var rule = CreateRule(MonitoringEvent.SpecimenOverdue, thresholdDays: 2, minimumPriority: PatientPathwayPriority.High);

        Find(rule).Should().Equal(overdue.Id);
    }

    [Test]
    public void WaitBreached_ZeroDays_IsARunningClockPastItsTarget()
    {
        var breached = AddReferral(status: ReferralStatus.Accepted, targetOn: Today.AddDays(-1));
        AddReferral(status: ReferralStatus.Accepted, targetOn: Today);
        AddReferral(status: ReferralStatus.Accepted, targetOn: Today.AddDays(-5), pausedSince: Today.AddDays(-2));
        AddReferral(status: ReferralStatus.Accepted, targetOn: Today.AddDays(-5), stoppedOn: Today.AddDays(-1));

        Find(CreateRule(MonitoringEvent.WaitBreached, thresholdDays: 0)).Should().Equal(breached.Id);
    }

    [Test]
    public void WaitBreached_SomeDays_IsWithinThatManyDaysOfItsTarget()
    {
        var near = AddReferral(status: ReferralStatus.Accepted, targetOn: Today.AddDays(2));
        AddReferral(status: ReferralStatus.Accepted, targetOn: Today.AddDays(3));

        Find(CreateRule(MonitoringEvent.WaitBreached, thresholdDays: 3)).Should().Equal(near.Id);
    }

    [Test]
    public void PauseReviewPassed_OpenPausePastItsReview_IsReportedByThePause()
    {
        var referral = AddReferral(status: ReferralStatus.Accepted, pausedSince: Today.AddDays(-20));
        var pause = new ReferralClockPause { Id = Guid.NewGuid(), Referral = referral, ReviewOn = Today.AddDays(-1) };
        _pauses.Add(pause);
        _pauses.Add(new ReferralClockPause { Id = Guid.NewGuid(), Referral = referral, ReviewOn = Today });
        _pauses.Add(new ReferralClockPause { Id = Guid.NewGuid(), Referral = referral, ReviewOn = Today.AddDays(-9), ResumedOn = Today.AddDays(-8) });

        Find(CreateRule(MonitoringEvent.PauseReviewPassed, thresholdDays: 0)).Should().Equal(pause.Id);
    }
}
