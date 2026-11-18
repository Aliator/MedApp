using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.DevMcp.DemoData;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Referrals;
using static MedApp.UnitTests.Common.Constants.DemoDataTestConstants;

namespace MedApp.UnitTests.Tools.DemoData;

/// <summary>
/// The demo's referrals as planned, before anything is replayed: each pathway's referral registered before its triage
/// and received on or before then, the few moved from another team, the pauses, and the referrals that never became
/// pathways (closed, or awaiting triage now) with their patients registered first.
/// </summary>
[TestFixture]
public sealed class ReferralPlansTests
{
    private DemoTimeline _timeline = null!;
    private List<PathwayPlan> _plans = null!;
    private DemoPatients _patients = null!;
    private List<ReferralStory> _stories = null!;

    [SetUp]
    public void SetUp()
    {
        var random = new Random(Seed);
        _timeline = new DemoTimeline(Now);
        _plans = PathwayPlans.Create(_timeline, random);
        _patients = new DemoPatients(_timeline, random);
        _patients.AssignPatients(_plans);
        _stories = ReferralStories.Plan(_plans, _patients, _timeline, random);
    }

    [Test]
    public void Plans_FromReferrals_AreRegisteredBeforeTheirTriage_AndReceivedByThen()
    {
        var referred = _plans.Where(p => p.FromReferral).ToList();

        referred.Count.Should().BeGreaterThan(_plans.Count * 8 / 10, "most pathways start from a referral");
        foreach (var plan in referred)
        {
            plan.RegisteredUtc.Should().BeBefore(plan.CreatedUtc);
            plan.ReceivedOn.Should().BeOnOrBefore(UkTime.DateOf(plan.RegisteredUtc));
            plan.Patient.CreatedUtc.Should().BeOnOrBefore(plan.RegisteredUtc, "the patient is added before their referral");
        }
    }

    [Test]
    public void Plans_WithoutAReferral_AreOnlyRecallsAndCoursesAskedForInClinic()
    {
        var direct = _plans.Where(p => !p.FromReferral).ToList();

        direct.Should().NotBeEmpty();
        direct.Should().OnlyContain(p => p.PathwayKind == PathwayKind.Diabetes || p.PathwayKind == PathwayKind.Biomechanics);
        direct.Should().OnlyContain(p => p.Pause == PauseFate.None && p.RedirectedFrom == null);
    }

    [Test]
    public void Plans_MovedFromAnotherTeam_AreMovedBetweenTheirRegistrationAndTriage()
    {
        var moved = _plans.Where(p => p.RedirectedFrom is not null).ToList();

        moved.Should().HaveCount(4);
        foreach (var plan in moved)
        {
            plan.RedirectedFrom.Should().NotBe(DemoTeams.For(plan.PathwayKind));
            plan.RedirectedUtc.Should().BeAfter(plan.RegisteredUtc).And.BeBefore(plan.CreatedUtc);
        }
    }

    [Test]
    public void Plans_Pauses_AreOnReferredPathways_BeforeTheirClockStops_WithEachReason()
    {
        var paused = _plans.Where(p => p.Pause != PauseFate.None).ToList();

        paused.Should().OnlyContain(p => p.FromReferral && p.PauseStage >= 0 && p.PauseStage <= p.Spec.ClockStopIndex);
        paused.Count(p => p.Pause == PauseFate.StillPaused).Should().Be(10);
        paused.Should().ContainSingle(p => p.Pause == PauseFate.ReviewOverdue);
        paused.Count(p => p.Pause == PauseFate.Resumed).Should().Be(10);
        paused.Select(p => p.PauseReason).Distinct().Should().BeEquivalentTo(Enum.GetValues<ClockPauseReason>());
    }

    [Test]
    public void Plans_RaisedAtTriage_AskedForLessThanUrgent()
    {
        var raised = _plans.Where(p => p.RaisePriority).ToList();

        raised.Should().NotBeEmpty();
        raised.Should().OnlyContain(p => p.ChosenPriority == PatientPathwayPriority.Urgent && p.RequestedPriority != PatientPathwayPriority.Urgent);
    }

    [Test]
    public void Stories_AreDecidedAfterRegistration_AndBeforeNow_OnPatientsAlreadyRegistered()
    {
        foreach (var story in _stories)
        {
            story.RegisteredUtc.Should().BeBefore(Now);
            story.ReceivedOn.Should().BeOnOrBefore(UkTime.DateOf(story.RegisteredUtc));
            if (story.Patient is { } patient)
                patient.CreatedUtc.Should().BeBefore(story.RegisteredUtc);
            else
                story.Duplicate.Should().BeTrue("only a duplicate's patient is chosen when it's played");

            if (story.Outcome == StoryOutcome.AwaitingTriage)
                continue;
            story.DecidedUtc.Should().BeAfter(story.RegisteredUtc).And.BeBefore(Now);
        }
    }

    [Test]
    public void Stories_CoverEveryOutcome_AndEveryRejectionReason()
    {
        _stories.Select(s => s.Outcome).Distinct().Should().BeEquivalentTo(Enum.GetValues<StoryOutcome>());
        _stories.Where(s => s.Outcome == StoryOutcome.Rejected).Select(s => s.RejectionReason).Distinct()
            .Should().BeEquivalentTo(Enum.GetValues<ReferralRejectionReason>());
        _stories.Should().ContainSingle(s => s.Outcome == StoryOutcome.EnteredInError);
        _stories.Count(s => s.Duplicate).Should().Be(2, "one rejected as a duplicate, one awaiting triage");
    }

    [Test]
    public void Stories_TheInbox_IsInEveryTeam_AndTheNewServicesComeAfterItWasAdded()
    {
        var inbox = _stories.Where(s => s.Outcome == StoryOutcome.AwaitingTriage).ToList();

        inbox.Count.Should().BeInRange(18, 25);
        inbox.Select(s => s.Team).Distinct()
            .Should().BeEquivalentTo(DemoTeams.Names.Append(DemoTeams.NewService));
        inbox.Where(s => s.Team == DemoTeams.NewService)
            .Should().HaveCount(3).And.OnlyContain(s => s.RegisteredUtc > _timeline.NewServiceAddedUtc && s.Kind == null);
        inbox.Should().ContainSingle(s => s.RedirectedFrom != null)
            .Which.RedirectedUtc.Should().BeAfter(inbox.Single(s => s.RedirectedFrom != null).RegisteredUtc);
    }
}
