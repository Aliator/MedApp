using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Activity.Repositories;
using MedApp.Domain.Activity;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.Extensions.Options;
using Moq;

namespace MedApp.UnitTests.Application.Activity.Common;

[TestFixture]
public sealed class ActivityLogTests
{
    private Mock<IActivityContext> _context = null!;
    private Mock<IActivityRepository> _repository = null!;
    private List<ActivityEntry> _added = null!;

    [SetUp]
    public void SetUp()
    {
        _context = new Mock<IActivityContext>();
        _context.Setup(c => c.Actor).Returns(ActivityTestConstants.Actor);
        _repository = new Mock<IActivityRepository>();
        _added = [];
        _repository
            .Setup(r => r.AddAsync(It.IsAny<IReadOnlyCollection<ActivityEntry>>(), It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyCollection<ActivityEntry> entries, CancellationToken _) => _added.AddRange(entries))
            .Returns(Task.CompletedTask);
        _repository
            .Setup(r => r.GetRecentlyRecordedPatientIdsAsync(
                It.IsAny<Guid?>(), It.IsAny<ActivityAction>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<Guid?>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid>());
    }

    private ActivityLog Log(int windowMinutes = 5) => new(
        _context.Object,
        _repository.Object,
        new FixedTimeProvider(ActivityTestConstants.Now),
        Options.Create(new ActivityOptions { ViewWindowMinutes = windowMinutes }));

    [Test]
    public void Actor_IsTheContexts()
    {
        Log().Actor.Should().Be(ActivityTestConstants.Actor);
    }

    [Test]
    public void Entry_ByTheRequestsActor_FillsEveryField()
    {
        var patientId = Guid.NewGuid();
        var procedureId = Guid.NewGuid();

        var entry = Log().Entry(ActivityAction.PatientUpdated, patientId, new { Fields = new[] { "email" }, Missing = (string?)null },
            "someone", procedureId);

        entry.Id.Should().NotBeEmpty();
        entry.AtUtc.Should().Be(ActivityTestConstants.Now);
        entry.Action.Should().Be(ActivityAction.PatientUpdated);
        entry.UserId.Should().Be(ActivityTestConstants.UserId);
        entry.Username.Should().Be(ActivityTestConstants.Username);
        entry.IpAddress.Should().Be(ActivityTestConstants.IpAddress);
        entry.PatientId.Should().Be(patientId);
        entry.ProcedureId.Should().Be(procedureId);
        entry.TargetUsername.Should().Be("someone");
        entry.Details.Should().Be("""{"fields":["email"]}""");
    }

    [Test]
    public void Entry_WithAnActor_UsesThem()
    {
        var actor = new ActivityActor(null, "tried-name", "198.51.100.7");

        var entry = Log().Entry(ActivityAction.SignInFailed, actor: actor);

        entry.UserId.Should().BeNull();
        entry.Username.Should().Be("tried-name");
        entry.IpAddress.Should().Be("198.51.100.7");
        entry.Details.Should().Be("{}");
        entry.PatientId.Should().BeNull();
    }

    [Test]
    public void Entry_LongValues_AreCutToTheirColumns()
    {
        var actor = new ActivityActor(null, new string('u', 300), new string('1', 60));

        var entry = Log().Entry(ActivityAction.SignInFailed, targetUsername: new string('t', 300), actor: actor);

        entry.Username.Should().HaveLength(ActivityLimits.Username);
        entry.TargetUsername.Should().HaveLength(ActivityLimits.Username);
        entry.IpAddress.Should().HaveLength(ActivityLimits.IpAddress);
    }

    [Test]
    public void Entry_NullUsername_IsEmpty()
    {
        var entry = Log().Entry(ActivityAction.SignedOut, actor: new ActivityActor(null, null!, null));

        entry.Username.Should().BeEmpty();
        entry.IpAddress.Should().BeNull();
    }

    [Test]
    public void SerializeDetails_NullIsEmptyObject_OtherwiseCamelCaseWithoutNulls()
    {
        ActivityLog.SerializeDetails(null).Should().Be("{}");
        ActivityLog.SerializeDetails(new { Reason = "Asked", Other = (string?)null }).Should().Be("""{"reason":"Asked"}""");
    }

    [Test]
    public async Task RecordAsync_AddsTheEntry_WithTheToken()
    {
        var log = Log();
        var entry = log.Entry(ActivityAction.SignedIn);
        using var cts = new CancellationTokenSource();

        await log.RecordAsync(entry, cts.Token);

        _repository.Verify(r => r.AddAsync(It.Is<IReadOnlyCollection<ActivityEntry>>(e => e.Single() == entry), cts.Token), Times.Once);
    }

    [Test]
    public async Task RecordViewsAsync_NoPatients_DoesNothing()
    {
        await Log().RecordViewsAsync(ActivityAction.PatientListed, [], CancellationToken.None);

        _repository.VerifyNoOtherCalls();
    }

    [Test]
    public async Task RecordViewsAsync_NewViews_RecordsOneEntryPerPatient_OnceEach()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        using var cts = new CancellationTokenSource();

        await Log().RecordViewsAsync(ActivityAction.PatientListed, [first, second, first], cts.Token);

        _added.Select(e => e.PatientId).Should().BeEquivalentTo(new Guid?[] { first, second });
        _added.Should().AllSatisfy(e =>
        {
            e.Action.Should().Be(ActivityAction.PatientListed);
            e.UserId.Should().Be(ActivityTestConstants.UserId);
            e.ProcedureId.Should().BeNull();
        });
        _repository.Verify(r => r.AddAsync(It.IsAny<IReadOnlyCollection<ActivityEntry>>(), cts.Token), Times.Once);
    }

    [Test]
    public async Task RecordViewsAsync_RecentViews_AreSkipped_AndTheLookupGetsTheWindow()
    {
        var seen = Guid.NewGuid();
        var unseen = Guid.NewGuid();
        var procedureId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        _repository
            .Setup(r => r.GetRecentlyRecordedPatientIdsAsync(
                ActivityTestConstants.UserId, ActivityAction.ProcedureViewed, It.IsAny<IReadOnlyCollection<Guid>>(), procedureId,
                ActivityTestConstants.Now.AddMinutes(-10), cts.Token))
            .ReturnsAsync(new HashSet<Guid> { seen });

        await Log(windowMinutes: 10).RecordViewsAsync(ActivityAction.ProcedureViewed, [seen, unseen], cts.Token, procedureId);

        _added.Should().ContainSingle().Which.PatientId.Should().Be(unseen);
        _added.Single().ProcedureId.Should().Be(procedureId);
    }

    [Test]
    public async Task RecordViewsAsync_AllRecent_AddsNothing()
    {
        var seen = Guid.NewGuid();
        _repository
            .Setup(r => r.GetRecentlyRecordedPatientIdsAsync(
                It.IsAny<Guid?>(), It.IsAny<ActivityAction>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<Guid?>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { seen });

        await Log().RecordViewsAsync(ActivityAction.PatientViewed, [seen], CancellationToken.None);

        _repository.Verify(r => r.AddAsync(It.IsAny<IReadOnlyCollection<ActivityEntry>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase(0)]
    [TestCase(-3)]
    public async Task RecordViewsAsync_NoWindow_RecordsEveryView_WithoutLookingUp(int window)
    {
        var patient = Guid.NewGuid();

        await Log(window).RecordViewsAsync(ActivityAction.PatientViewed, [patient], CancellationToken.None);

        _added.Should().ContainSingle().Which.PatientId.Should().Be(patient);
        _repository.Verify(r => r.GetRecentlyRecordedPatientIdsAsync(
            It.IsAny<Guid?>(), It.IsAny<ActivityAction>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<Guid?>(),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
