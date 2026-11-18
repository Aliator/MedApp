using FluentAssertions;
using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Referrals.Commands.AcceptReferral;
using MedApp.Application.Referrals.Commands.RedirectReferral;
using MedApp.Application.Referrals.Commands.RejectReferral;
using MedApp.Application.Referrals.Common;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Referrals;
using MedApp.Domain.Specialties;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ReferralsTestConstants;

namespace MedApp.UnitTests.Application.Referrals.Commands;

/// <summary>Shared mocks for triage: the triager belongs to the referral's specialty, and notifications are kept.</summary>
public abstract class TriageCommandTestBase : ReferralCommandTestBase
{
    protected Mock<INotificationSender> Notifications { get; private set; } = null!;
    protected List<Notification> Sent { get; } = [];

    [SetUp]
    public void SetUpTriageMocks()
    {
        Sent.Clear();
        Notifications = new Mock<INotificationSender>();
        Notifications
            .Setup(n => n.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback((Notification notification, CancellationToken _) => Sent.Add(notification))
            .Returns(Task.CompletedTask);
        Repository
            .Setup(r => r.IsSpecialtyMemberAsync(It.IsAny<Guid>(), Specialty.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    /// <summary>Saving finds the referral changed meanwhile, decided by someone else as <paramref name="status"/>.</summary>
    protected void GivenSomeoneElseDecidedItFirst(Referral referral, ReferralStatus status)
    {
        var decided = CreateReferral(Patient, Specialty, status);
        decided.DecidedByUsername = "o.synthetic";
        decided.DecidedAtUtc = NowUtc;
        Repository.Setup(r => r.GetByIdAsync(referral.Id, It.IsAny<CancellationToken>())).ReturnsAsync(decided);
        Repository
            .Setup(r => r.SaveAsync(It.IsAny<IEnumerable<ReferralEvent>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException(ReferralWorkflow.ChangedMeanwhileMessage));
    }
}

[TestFixture]
public sealed class AcceptReferralHandlerTests : TriageCommandTestBase
{
    private Mock<IPatientPathwayRepository> _pathways = null!;
    private Mock<IPatientPathwayStagesRepository> _stages = null!;
    private Mock<IIdentityReadService> _identity = null!;
    private Mock<IPermissionHolders> _holders = null!;
    private Mock<IPathwayMonitor> _monitor = null!;
    private PathwayTemplate _template = null!;
    private readonly List<PathwayChange> _added = [];
    private readonly List<string> _order = [];

    [SetUp]
    public void SetUp()
    {
        _added.Clear();
        _order.Clear();
        _template = PathwaysTestConstants.CreateTemplate(
            PathwaysTestConstants.TemplateName, false,
            PathwaysTestConstants.CreateDefinition(PathwaysTestConstants.ConsultationStage).WithVisit(VisitRequirement.Required),
            PathwaysTestConstants.CreateDefinition(PathwaysTestConstants.PathwayStage));
        _template.SpecialtyId = Specialty.Id;

        _pathways = new Mock<IPatientPathwayRepository>();
        _pathways
            .Setup(r => r.AddAsync(It.IsAny<PathwayChange>(), It.IsAny<CancellationToken>()))
            .Callback((PathwayChange change, CancellationToken ct) =>
            {
                _added.Add(change);
                Tokens.Add(ct);
                _order.Add("save");
            })
            .Returns(Task.CompletedTask);
        Repository
            .Setup(r => r.AddEvents(It.IsAny<IEnumerable<ReferralEvent>>()))
            .Callback((IEnumerable<ReferralEvent> events) => Saved.AddRange(events));
        _stages = new Mock<IPatientPathwayStagesRepository>();
        _stages.Setup(s => s.GetPathwayTemplateByIdAsync(_template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_template);
        _identity = new Mock<IIdentityReadService>();
        _identity
            .Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                (IReadOnlyDictionary<Guid, string>)ids.Where(id => id == AssigneeId).ToDictionary(id => id, _ => AssigneeUsername));
        _holders = new Mock<IPermissionHolders>();
        _holders
            .Setup(h => h.WhoHoldsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), Permission.WorkPathways, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, Permission _, CancellationToken _) => (IReadOnlySet<Guid>)ids.ToHashSet());
        _monitor = new Mock<IPathwayMonitor>();
        _monitor
            .Setup(m => m.NotifyAsync(It.IsAny<PathwayChange>(), It.IsAny<CancellationToken>()))
            .Callback(() => _order.Add("monitor"))
            .Returns(Task.CompletedTask);
        Notifications
            .Setup(n => n.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback((Notification notification, CancellationToken _) =>
            {
                Sent.Add(notification);
                _order.Add($"notify {notification.Kind}");
            })
            .Returns(Task.CompletedTask);
    }

    private AcceptReferralHandler Handler => new(
        Repository.Object, _pathways.Object, _stages.Object, _identity.Object, _holders.Object,
        Notifications.Object, _monitor.Object, Clock);

    private AcceptReferralCommand Command(Referral referral, PathwayActor? actor = null, Guid? assignee = null, string? notes = null) =>
        new(referral.Id, _template.Id, "Urgent", Today.AddDays(14), null, assignee, notes, referral.Version, actor ?? Triager);

    [Test]
    public async Task Handle_Valid_CreatesThePathwayAndAcceptsTheReferralInOneSave()
    {
        var referral = GivenReferral();

        var response = await Handler.Handle(Command(referral, notes: "First synthetic note"), Cts.Token);

        var change = _added.Single();
        var pathway = change.Pathway;
        pathway.PatientId.Should().Be(Patient.Id);
        pathway.SpecialtyId.Should().Be(Specialty.Id);
        pathway.Priority.Should().Be(PatientPathwayPriority.Urgent);
        pathway.Title.Should().Be(PathwaysTestConstants.TemplateName);
        pathway.Stages.Should().HaveCount(2);

        // The pathway's creation is recorded as any new pathway's, with the referral it came from.
        var created = change.AuditEntries.First();
        created.Action.Should().Be(PatientPathwayAuditAction.Created);
        created.Details.Should().Contain(referral.Id.ToString());
        change.NewNotes.Should().ContainSingle();
        change.NewAppointments.Should().ContainSingle("the first stage involves a visit");

        referral.Status.Should().Be(ReferralStatus.Accepted);
        referral.PathwayId.Should().Be(pathway.Id);
        referral.DecidedByUsername.Should().Be(Triager.Username);
        referral.DecidedAtUtc.Should().Be(NowUtc);
        referral.Version.Should().Be(4);
        var accepted = Saved.Single();
        accepted.Action.Should().Be(ReferralEventAction.Accepted);
        accepted.PathwayId.Should().Be(pathway.Id);
        Repository.Verify(r => r.SaveAsync(It.IsAny<IEnumerable<ReferralEvent>>(), It.IsAny<CancellationToken>()), Times.Never);
        Tokens.Should().Equal(Cts.Token);

        response.Id.Should().Be(pathway.Id);
        response.Referral!.Id.Should().Be(referral.Id);
        response.Referral.SpecialtyName.Should().Be(Specialty.Name);
    }

    [Test]
    public async Task Handle_Valid_TellsTheRegistrarThenRunsTheMonitor()
    {
        var referral = GivenReferral();

        await Handler.Handle(Command(referral), Cts.Token);

        var told = Sent.Single();
        told.Kind.Should().Be(NotificationKind.ReferralTriaged);
        told.UserId.Should().Be(Registrar.UserId);
        told.ReferralId.Should().Be(referral.Id);
        told.Title.Should().Be(ReferralNotifications.TriagedTitle);
        told.Message.Should().Be($"{Triager.Username} accepted a {Specialty.Name} referral.");
        _order.Should().Equal("save", "notify ReferralTriaged", "monitor");
        _monitor.Verify(m => m.NotifyAsync(_added.Single(), Cts.Token), Times.Once);
    }

    [Test]
    public async Task Handle_WithAnAssignee_AssignsAndTellsThemBeforeTheRegistrar()
    {
        var referral = GivenReferral();

        var response = await Handler.Handle(Command(referral, AssigningTriager, AssigneeId), Cts.Token);

        var pathway = _added.Single().Pathway;
        pathway.Assignments.Single().UserId.Should().Be(AssigneeId);
        pathway.Status.Should().Be(PatientPathwayStatus.Assigned);
        _order.Should().Equal("save", "notify PathwayAssigned", "notify ReferralTriaged", "monitor");
        response.Assignments.Single().Username.Should().Be(AssigneeUsername);
    }

    [Test]
    public async Task Handle_AssigneeWithoutAssignPathways_IsForbiddenAndSavesNothing()
    {
        var referral = GivenReferral();

        var act = () => Handler.Handle(Command(referral, Triager, AssigneeId), Cts.Token);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage(AcceptReferralHandler.CantAssignMessage);
        _added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_AssigneeWhoCantWorkOnPathways_IsAValidationError()
    {
        var referral = GivenReferral();
        _holders
            .Setup(h => h.WhoHoldsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), Permission.WorkPathways, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid>());

        var act = () => Handler.Handle(Command(referral, AssigningTriager, AssigneeId), Cts.Token);

        await act.Should().ThrowAsync<ValidationException>();
        _added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_UnknownAssignee_IsNotFound()
    {
        var referral = GivenReferral();

        var act = () => Handler.Handle(Command(referral, AssigningTriager, Guid.NewGuid()), Cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_TheRegistrarTriagesIt_TellsNobody()
    {
        var referral = GivenReferral();
        var registrarTriager = new PathwayActor(Registrar.UserId, Registrar.Username, StandardRoles.Clinician.Permissions);

        await Handler.Handle(Command(referral, registrarTriager), Cts.Token);

        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_AnotherSpecialtysTemplate_IsAValidationErrorSayingToRedirectFirst()
    {
        var referral = GivenReferral();
        _template.SpecialtyId = Guid.NewGuid();

        var act = () => Handler.Handle(Command(referral), Cts.Token);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Single().ErrorMessage
            .Should().Be(ReferralWorkflow.TemplateNotInSpecialtyMessage(PathwaysTestConstants.TemplateName, Specialty.Name));
        _added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ArchivedTemplate_IsAConflict()
    {
        var referral = GivenReferral();
        _template.IsArchived = true;

        var act = () => Handler.Handle(Command(referral), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*archived*");
    }

    [Test]
    public async Task Handle_TemplateWithNoStages_IsAConflict()
    {
        var referral = GivenReferral();
        _template.Maps.Clear();

        var act = () => Handler.Handle(Command(referral), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*no stages*");
    }

    [Test]
    public async Task Handle_UnknownTemplate_IsNotFound()
    {
        var referral = GivenReferral();

        var act = () => Handler.Handle(Command(referral) with { TemplateId = Guid.NewGuid() }, Cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_NotAMemberOfItsSpecialty_IsForbidden()
    {
        var referral = GivenReferral();
        Repository
            .Setup(r => r.IsSpecialtyMemberAsync(Triager.UserId, Specialty.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = () => Handler.Handle(Command(referral), Cts.Token);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage(ReferralWorkflow.NotInSpecialtyMessage(Specialty.Name));
        _added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_WithoutTheTriagePermission_IsForbidden()
    {
        var referral = GivenReferral();

        var act = () => Handler.Handle(Command(referral, Registrar), Cts.Token);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage(ReferralWorkflow.NoTriagePermissionMessage);
    }

    [Test]
    public async Task Handle_AlreadyTriaged_IsAConflictNamingWhoAndHow()
    {
        var referral = GivenReferral(ReferralStatus.Rejected);
        referral.DecidedByUsername = "o.synthetic";
        referral.DecidedAtUtc = NowUtc;

        var act = () => Handler.Handle(Command(referral), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*o.synthetic rejected it on 18 November 2026*");
    }

    [Test]
    public async Task Handle_RestrictedRecord_IsAConflict()
    {
        var referral = GivenReferral();
        Patient.RestrictedAtUtc = NowUtc;

        var act = () => Handler.Handle(Command(referral), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>();
        _added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_StaleVersion_IsAConflict()
    {
        var referral = GivenReferral();

        var act = () => Handler.Handle(Command(referral) with { ExpectedVersion = 2 }, Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(ReferralWorkflow.ChangedMeanwhileMessage);
    }

    [Test]
    public async Task Handle_AnotherTriagerSavedFirst_IsAConflictNamingThemAndTellsNobody()
    {
        var referral = GivenReferral();
        GivenSomeoneElseDecidedItFirst(referral, ReferralStatus.Accepted);
        _pathways
            .Setup(r => r.AddAsync(It.IsAny<PathwayChange>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("refused"));

        var act = () => Handler.Handle(Command(referral), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*o.synthetic accepted it into a pathway*");
        Sent.Should().BeEmpty();
        _monitor.Verify(m => m.NotifyAsync(It.IsAny<PathwayChange>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_SavedMeanwhileButStillOpen_IsChangedMeanwhile()
    {
        var referral = GivenReferral();
        Repository.Setup(r => r.GetByIdAsync(referral.Id, It.IsAny<CancellationToken>())).ReturnsAsync(CreateReferral(Patient, Specialty));
        _pathways
            .Setup(r => r.AddAsync(It.IsAny<PathwayChange>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("refused"));

        var act = () => Handler.Handle(Command(referral), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(ReferralWorkflow.ChangedMeanwhileMessage);
    }

    [Test]
    public async Task Handle_UnknownReferral_IsNotFound()
    {
        var act = () => Handler.Handle(new AcceptReferralCommand(
            Guid.NewGuid(), _template.Id, "Low", Today, null, null, null, 1, Triager), Cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}

[TestFixture]
public sealed class RejectReferralHandlerTests : TriageCommandTestBase
{
    private RejectReferralHandler Handler => new(Repository.Object, Notifications.Object, Clock);

    [Test]
    public async Task Handle_Valid_RejectsItWithTheReasonAndDetailAndTellsTheRegistrar()
    {
        var referral = GivenReferral();

        var response = await Handler.Handle(
            new RejectReferralCommand(referral.Id, "insufficientInformation", $"  {Detail} ", referral.Version, Triager), Cts.Token);

        referral.Status.Should().Be(ReferralStatus.Rejected);
        referral.RejectionReason.Should().Be(ReferralRejectionReason.InsufficientInformation);
        referral.DecisionDetail.Should().Be(Detail);
        referral.DecidedByUsername.Should().Be(Triager.Username);
        var entry = Saved.Single();
        entry.Action.Should().Be(ReferralEventAction.Rejected);
        entry.Details.Should().Contain("InsufficientInformation").And.NotContain(Detail);
        Tokens.Should().Equal(Cts.Token);
        Sent.Single().Message.Should().Be($"{Triager.Username} rejected a {Specialty.Name} referral.");
        response.Status.Should().Be("Rejected");
        response.CanTriage.Should().BeFalse();
    }

    [Test]
    public async Task Handle_NoDetail_KeepsNone()
    {
        var referral = GivenReferral();

        await Handler.Handle(new RejectReferralCommand(referral.Id, "Duplicate", "  ", referral.Version, Triager), Cts.Token);

        referral.DecisionDetail.Should().BeNull();
    }

    [Test]
    public async Task Handle_AnotherTriagerSavedFirst_IsAConflictNamingThem()
    {
        var referral = GivenReferral();
        GivenSomeoneElseDecidedItFirst(referral, ReferralStatus.Rejected);

        var act = () => Handler.Handle(new RejectReferralCommand(referral.Id, "Duplicate", null, referral.Version, Triager), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*o.synthetic rejected it*");
        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_NotAMember_IsForbidden()
    {
        var referral = GivenReferral();
        Repository
            .Setup(r => r.IsSpecialtyMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = () => Handler.Handle(new RejectReferralCommand(referral.Id, "Duplicate", null, referral.Version, Triager), Cts.Token);

        await act.Should().ThrowAsync<ForbiddenException>();
        Saved.Should().BeEmpty();
    }
}

[TestFixture]
public sealed class RedirectReferralHandlerTests : TriageCommandTestBase
{
    private Specialty _urology = null!;

    [SetUp]
    public void SetUp()
    {
        _urology = SpecialtiesTestConstants.CreateSpecialty(SpecialtiesTestConstants.OtherSpecialtyName);
        Specialties.Setup(s => s.GetAsync(_urology.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_urology);
    }

    private RedirectReferralHandler Handler => new(Repository.Object, Specialties.Object, Notifications.Object, Clock);

    [Test]
    public async Task Handle_ToAnotherSpecialty_KeepsItAwaitingTriageThere()
    {
        var referral = GivenReferral();

        var response = await Handler.Handle(
            new RedirectReferralCommand(referral.Id, _urology.Id, null, $" {Detail} ", referral.Version, Triager), Cts.Token);

        referral.Status.Should().Be(ReferralStatus.AwaitingTriage);
        referral.SpecialtyId.Should().Be(_urology.Id);
        referral.RedirectDetail.Should().Be(Detail);
        referral.DecidedAtUtc.Should().BeNull();
        referral.ReceivedOn.Should().Be(ReceivedOn);
        var entry = Saved.Single();
        entry.Action.Should().Be(ReferralEventAction.SpecialtyChanged);
        entry.Details.Should().Contain(_urology.Name).And.Contain(Specialty.Name).And.NotContain(Detail);
        Sent.Single().Message.Should().Be($"{Triager.Username} redirected a {Specialty.Name} referral to {_urology.Name}.");
        response.SpecialtyName.Should().Be(_urology.Name);
        response.IsOpen.Should().BeTrue();
    }

    [Test]
    public async Task Handle_Elsewhere_ClosesItKeepingWhereAndWhyOnTheReferral()
    {
        var referral = GivenReferral();

        await Handler.Handle(
            new RedirectReferralCommand(referral.Id, null, " Synthetic Community Trust ", Detail, referral.Version, Triager), Cts.Token);

        referral.Status.Should().Be(ReferralStatus.RedirectedElsewhere);
        referral.RedirectedTo.Should().Be("Synthetic Community Trust");
        referral.DecisionDetail.Should().Be(Detail);
        var entry = Saved.Single();
        entry.Action.Should().Be(ReferralEventAction.RedirectedElsewhere);
        entry.Details.Should().NotContain("Synthetic Community Trust").And.NotContain(Detail);
        Sent.Single().Message.Should().Be($"{Triager.Username} redirected a {Specialty.Name} referral elsewhere.");
    }

    [Test]
    public async Task Handle_ItsOwnSpecialty_IsAValidationError()
    {
        var referral = GivenReferral();

        var act = () => Handler.Handle(new RedirectReferralCommand(referral.Id, Specialty.Id, null, Detail, referral.Version, Triager), Cts.Token);

        await act.Should().ThrowAsync<ValidationException>();
        Saved.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ArchivedSpecialty_IsAConflict()
    {
        var referral = GivenReferral();
        _urology.IsArchived = true;

        var act = () => Handler.Handle(new RedirectReferralCommand(referral.Id, _urology.Id, null, Detail, referral.Version, Triager), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(ReferralWorkflow.ArchivedSpecialtyMessage(_urology.Name));
    }

    [Test]
    public async Task Handle_UnknownSpecialty_IsNotFound()
    {
        var referral = GivenReferral();

        var act = () => Handler.Handle(new RedirectReferralCommand(referral.Id, Guid.NewGuid(), null, Detail, referral.Version, Triager), Cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_AnotherTriagerSavedFirst_IsAConflictNamingThem()
    {
        var referral = GivenReferral();
        GivenSomeoneElseDecidedItFirst(referral, ReferralStatus.Accepted);

        var act = () => Handler.Handle(new RedirectReferralCommand(referral.Id, null, "Elsewhere", Detail, referral.Version, Triager), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*o.synthetic accepted it*");
    }
}

[TestFixture]
public sealed class TriageValidatorTests
{
    private readonly FixedTimeProvider _clock = new(NowUtc);

    [Test]
    public void Accept_Valid_Passes() =>
        new AcceptReferralValidator(_clock)
            .Validate(new AcceptReferralCommand(Guid.NewGuid(), Guid.NewGuid(), "High", Today, " ", null, null, 1, Triager))
            .IsValid.Should().BeTrue();

    [Test]
    public void Accept_NothingChosen_NamesEachChoice()
    {
        var result = new AcceptReferralValidator(_clock)
            .Validate(new AcceptReferralCommand(Guid.NewGuid(), Guid.Empty, "", default, null, Guid.Empty, null, 0, Triager));

        result.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(
            [nameof(AcceptReferralCommand.TemplateId), nameof(AcceptReferralCommand.Priority), nameof(AcceptReferralCommand.DueDate),
             nameof(AcceptReferralCommand.AssigneeUserId), nameof(AcceptReferralCommand.ExpectedVersion)]);
    }

    [Test]
    public void Accept_PastTargetDateLongTitleAndNotes_AreRefused()
    {
        var result = new AcceptReferralValidator(_clock).Validate(new AcceptReferralCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Low", Today.AddDays(-1), new string('t', 201), null,
            new string('n', PatientPathwayNote.MaxTextLength + 1), 1, Triager));

        result.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(["DueDate", "Title", "Notes"]);
    }

    [TestCase("Duplicate", null, true)]
    [TestCase("other", null, false)]
    [TestCase("Other", "  ", false)]
    [TestCase("Other", "Synthetic: seen privately", true)]
    [TestCase("Maybe", null, false)]
    [TestCase("", null, false)]
    public void Reject_ReasonAndDetail(string reason, string? detail, bool valid) =>
        new RejectReferralValidator()
            .Validate(new RejectReferralCommand(Guid.NewGuid(), reason, detail, 1, Triager))
            .IsValid.Should().Be(valid);

    [Test]
    public void Reject_LongDetail_IsRefused() =>
        new RejectReferralValidator()
            .Validate(new RejectReferralCommand(Guid.NewGuid(), "Duplicate", new string('d', Referral.MaxDetailLength + 1), 1, Triager))
            .IsValid.Should().BeFalse();

    [Test]
    public void Redirect_ToASpecialty_Passes() =>
        new RedirectReferralValidator()
            .Validate(new RedirectReferralCommand(Guid.NewGuid(), Guid.NewGuid(), null, Detail, 1, Triager))
            .IsValid.Should().BeTrue();

    [Test]
    public void Redirect_Elsewhere_Passes() =>
        new RedirectReferralValidator()
            .Validate(new RedirectReferralCommand(Guid.NewGuid(), null, "Synthetic Trust", Detail, 1, Triager))
            .IsValid.Should().BeTrue();

    [Test]
    public void Redirect_BothOrNeither_IsRefused()
    {
        var validator = new RedirectReferralValidator();

        validator.Validate(new RedirectReferralCommand(Guid.NewGuid(), Guid.NewGuid(), "Synthetic Trust", Detail, 1, Triager))
            .Errors.Single().ErrorMessage.Should().Be(RedirectReferralValidator.OneDestinationMessage);
        validator.Validate(new RedirectReferralCommand(Guid.NewGuid(), null, " ", Detail, 1, Triager))
            .Errors.Single().PropertyName.Should().Be(nameof(RedirectReferralCommand.SpecialtyId));
    }

    [Test]
    public void Redirect_NoDetailOrTooLong_IsRefused()
    {
        var validator = new RedirectReferralValidator();

        validator.Validate(new RedirectReferralCommand(Guid.NewGuid(), Guid.NewGuid(), null, " ", 1, Triager))
            .Errors.Single().ErrorMessage.Should().Be(RedirectReferralValidator.DetailRequiredMessage);
        validator.Validate(new RedirectReferralCommand(Guid.NewGuid(), null, new string('e', Referral.MaxRedirectedToLength + 1), Detail, 0, Triager))
            .Errors.Should().HaveCount(2);
    }
}
