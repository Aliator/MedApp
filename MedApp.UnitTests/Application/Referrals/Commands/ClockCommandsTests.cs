using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Referrals.Commands.PauseClock;
using MedApp.Application.Referrals.Commands.ResumeClock;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Referrals.Repositories;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Referrals;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Referrals.Commands;

/// <summary>
/// Pausing and resuming a referral's waiting-time clock: through the pathway's change, so the pause, the referral's
/// entry and the pathway's target date are saved together, then the monitor hears of the target date change.
/// </summary>
[TestFixture]
public sealed class ClockCommandsTests
{
    private CancellationTokenSource _cts = null!;
    private Mock<IReferralRepository> _referrals = null!;
    private Mock<IPatientPathwayRepository> _pathways = null!;
    private Mock<IPathwayMonitor> _monitor = null!;
    private List<string> _order = null!;
    private PathwayChange? _saved;
    private PatientPathway _pathway = null!;
    private Referral _referral = null!;

    private static readonly PathwayActor Worker = new(Staff.UserId, Staff.Username, TestPermissions.Staff);

    [SetUp]
    public void SetUp()
    {
        _cts = new CancellationTokenSource();
        _referrals = new Mock<IReferralRepository>();
        _pathways = new Mock<IPatientPathwayRepository>();
        _monitor = new Mock<IPathwayMonitor>();
        _order = [];
        _saved = null;

        _pathway = CreatePathway(PatientPathwayStatus.InProgress, priority: PatientPathwayPriority.High);
        _referral = ReferralsTestConstants.CreateReferral(patient: _pathway.Patient, status: ReferralStatus.Accepted, version: 3);
        _referral.ReceivedOn = Today.AddDays(-10);
        WaitingClock.Start(_referral, 28);
        _referral.PathwayId = _pathway.Id;
        _pathway.Referral = _referral;
        _pathway.DueDateUtc = DueDates.EndOfDayUtc(_referral.TargetOn);

        _referrals.Setup(r => r.GetForUpdateAsync(_referral.Id, _cts.Token)).ReturnsAsync(_referral);
        _pathways.Setup(r => r.GetForUpdateAsync(_pathway.Id, _cts.Token)).ReturnsAsync(_pathway);
        _pathways.Setup(r => r.SaveAsync(It.IsAny<PathwayChange>(), _cts.Token))
            .Callback((PathwayChange change, CancellationToken _) =>
            {
                _saved = change;
                _order.Add("save");
            })
            .Returns(Task.CompletedTask);
        _monitor.Setup(m => m.NotifyAsync(It.IsAny<PathwayChange>(), _cts.Token))
            .Callback(() => _order.Add("monitor"))
            .Returns(Task.CompletedTask);
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    private PauseClockHandler PauseHandler => new(_referrals.Object, _pathways.Object, _monitor.Object, new FixedTimeProvider(Now));

    private ResumeClockHandler ResumeHandler(DateTime now) => new(_referrals.Object, _pathways.Object, _monitor.Object, new FixedTimeProvider(now));

    private PauseClockCommand Pause(string reason = "patientChoseToWait", string? detail = null, int version = 3) =>
        new(_referral.Id, reason, detail, Today.AddDays(30), version, Worker);

    [Test]
    public async Task Pause_Valid_PausesMovesTheTargetDateSavesOnceThenRunsTheMonitor()
    {
        var card = await PauseHandler.Handle(Pause(detail: "  Synthetic: away until the new year  "), _cts.Token);

        _referral.PausedSinceOn.Should().Be(Today);
        _referral.Version.Should().Be(4);
        var pause = _saved!.NewPauses.Single();
        pause.Reason.Should().Be(ClockPauseReason.PatientChoseToWait);
        pause.Detail.Should().Be("Synthetic: away until the new year");
        DueDates.ToUkDate(_pathway.DueDateUtc).Should().Be(Today.AddDays(30));
        _saved.ReferralEvents.Single().Action.Should().Be(ReferralEventAction.ClockPaused);
        _saved.AuditEntries.Single().Details.Should().Contain("\"source\":\"clock\"");
        _order.Should().Equal("save", "monitor");
        _monitor.Verify(m => m.NotifyAsync(_saved, _cts.Token), Times.Once);

        card.Id.Should().Be(_referral.Id);
        card.Version.Should().Be(4);
        card.Clock!.State.Should().Be(WaitingClock.Paused);
        card.Pauses.Should().ContainSingle();
        card.CanPauseClock.Should().BeFalse();
        card.CanResumeClock.Should().BeTrue();
    }

    [Test]
    public async Task Pause_Unknown_IsNotFound()
    {
        var act = () => PauseHandler.Handle(Pause() with { ReferralId = Guid.NewGuid() }, _cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Pause_BeforeTriage_IsAConflict()
    {
        _referral.Status = ReferralStatus.AwaitingTriage;

        var act = () => PauseHandler.Handle(Pause(), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(WaitingClock.NotAcceptedMessage);
        _pathways.Verify(r => r.SaveAsync(It.IsAny<PathwayChange>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Pause_WithoutAPathway_IsAConflict()
    {
        _referral.PathwayId = null;

        var act = () => PauseHandler.Handle(Pause(), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(WaitingClock.StoppedMessage);
    }

    [Test]
    public async Task Pause_ChangedSince_IsAConflict()
    {
        var act = () => PauseHandler.Handle(Pause(version: 2), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(ReferralWorkflow.ChangedMeanwhileMessage);
    }

    [Test]
    public async Task Pause_WhileRestricted_IsAllowed()
    {
        _pathway.Patient.RestrictedAtUtc = Now.AddDays(-1);

        await PauseHandler.Handle(Pause(), _cts.Token);

        _referral.PausedSinceOn.Should().Be(Today);
    }

    [Test]
    public async Task Pause_OnceAnonymised_IsAConflict()
    {
        _pathway.Patient.AnonymisedAtUtc = Now.AddDays(-1);

        var act = () => PauseHandler.Handle(Pause(), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Resume_Valid_AddsThePausedDaysAndMovesTheTargetDateBack()
    {
        await PauseHandler.Handle(Pause(), _cts.Token);
        _order.Clear();

        var card = await ResumeHandler(Now.AddDays(5)).Handle(new ResumeClockCommand(_referral.Id, 4, Worker), _cts.Token);

        _referral.PausedDays.Should().Be(5);
        DueDates.ToUkDate(_pathway.DueDateUtc).Should().Be(Today.AddDays(18 + 5));
        _saved!.ReferralEvents.Single().Action.Should().Be(ReferralEventAction.ClockResumed);
        _order.Should().Equal("save", "monitor");
        card.Clock!.State.Should().Be(WaitingClock.Running);
        card.CanPauseClock.Should().BeTrue();
    }

    [Test]
    public async Task Resume_NotPaused_IsAConflict()
    {
        var act = () => ResumeHandler(Now).Handle(new ResumeClockCommand(_referral.Id, 3, Worker), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(WaitingClock.NotPausedMessage);
    }

    [Test]
    public async Task Resume_Unknown_IsNotFound_AndWithoutAPathwayAConflict()
    {
        var unknown = () => ResumeHandler(Now).Handle(new ResumeClockCommand(Guid.NewGuid(), 3, Worker), _cts.Token);
        await unknown.Should().ThrowAsync<NotFoundException>();

        await PauseHandler.Handle(Pause(), _cts.Token);
        _referral.PathwayId = null;
        var detached = () => ResumeHandler(Now).Handle(new ResumeClockCommand(_referral.Id, 4, Worker), _cts.Token);
        await detached.Should().ThrowAsync<ConflictException>().WithMessage(WaitingClock.StoppedMessage);
    }

    [Test]
    public async Task Resume_WhileRestricted_IsAConflict()
    {
        await PauseHandler.Handle(Pause(), _cts.Token);
        _pathway.Patient.RestrictedAtUtc = Now;

        var act = () => ResumeHandler(Now).Handle(new ResumeClockCommand(_referral.Id, 4, Worker), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>();
    }
}

[TestFixture]
public sealed class ClockValidatorTests
{
    private static readonly PathwayActor Worker = new(Staff.UserId, Staff.Username, TestPermissions.Staff);
    private readonly PauseClockValidator _pause = new(new FixedTimeProvider(Now));

    private static PauseClockCommand Pause(string reason = "PatientUnavailable", string? detail = null, int days = 1, int version = 1) =>
        new(Guid.NewGuid(), reason, detail, Today.AddDays(days), version, Worker);

    [Test]
    public void Pause_Valid_Passes()
    {
        _pause.Validate(Pause()).IsValid.Should().BeTrue();
        _pause.Validate(Pause(days: 90)).IsValid.Should().BeTrue();
        _pause.Validate(Pause("Other", "Synthetic detail")).IsValid.Should().BeTrue();
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(91)]
    public void Pause_ReviewDateOutOfRange_IsRefused(int days) =>
        _pause.Validate(Pause(days: days)).Errors.Single().ErrorMessage.Should().Be(PauseClockValidator.ReviewOnMessage);

    [Test]
    public void Pause_UnknownReason_IsRefused() =>
        _pause.Validate(Pause("OnHold")).Errors.Single().ErrorMessage.Should().Be(PauseClockValidator.ReasonMessage);

    [Test]
    public void Pause_OtherWithoutADetail_IsRefused() =>
        _pause.Validate(Pause("Other", " ")).Errors.Single().ErrorMessage.Should().Be(PauseClockValidator.DetailRequiredMessage);

    [Test]
    public void Pause_LongDetailNoIdOrVersion_AreRefused()
    {
        var command = Pause(detail: new string('d', ReferralClockPause.MaxDetailLength + 1), version: 0) with { ReferralId = Guid.Empty };

        _pause.Validate(command).Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(
            [nameof(PauseClockCommand.ReferralId), nameof(PauseClockCommand.Detail), nameof(PauseClockCommand.ExpectedVersion)]);
    }

    [Test]
    public void Resume_NeedsTheReferralAndTheVersion()
    {
        var validator = new ResumeClockValidator();

        validator.Validate(new ResumeClockCommand(Guid.NewGuid(), 1, Worker)).IsValid.Should().BeTrue();
        validator.Validate(new ResumeClockCommand(Guid.Empty, 0, Worker)).Errors.Should().HaveCount(2);
    }
}
