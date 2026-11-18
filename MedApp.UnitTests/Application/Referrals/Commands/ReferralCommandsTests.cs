using FluentAssertions;
using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Referrals.Commands.EditReferral;
using MedApp.Application.Referrals.Commands.MarkReferralEnteredInError;
using MedApp.Application.Referrals.Commands.RegisterReferral;
using MedApp.Application.Referrals.Commands.WithdrawReferral;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Referrals.Repositories;
using MedApp.Application.Specialties.Repositories;
using MedApp.Domain.Patients;
using MedApp.Domain.Referrals;
using MedApp.Domain.Specialties;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ReferralsTestConstants;

namespace MedApp.UnitTests.Application.Referrals.Commands;

/// <summary>Shared mocks for the referral command tests: a repository that records what it's asked to save.</summary>
public abstract class ReferralCommandTestBase
{
    protected Mock<IReferralRepository> Repository { get; private set; } = null!;
    protected Mock<IPatientRepository> Patients { get; private set; } = null!;
    protected Mock<ISpecialtyRepository> Specialties { get; set; } = null!;
    protected FixedTimeProvider Clock { get; } = new(NowUtc);
    protected List<Referral> Added { get; } = [];
    protected List<ReferralEvent> Saved { get; } = [];
    protected List<CancellationToken> Tokens { get; } = [];
    protected Specialty Specialty { get; private set; } = null!;
    protected Patient Patient { get; private set; } = null!;
    protected CancellationTokenSource Cts { get; private set; } = null!;

    [SetUp]
    public void SetUpReferralMocks()
    {
        Cts = new CancellationTokenSource();
        Added.Clear();
        Saved.Clear();
        Tokens.Clear();
        Specialty = SpecialtiesTestConstants.CreateSpecialty();
        Patient = PatientsTestConstants.CreateValidPatient();
        Specialties = SpecialtiesTestConstants.RepositoryWith(Specialty);
        Patients = new Mock<IPatientRepository>();
        Patients.Setup(p => p.GetByIdAsync(Patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Patient);
        Repository = new Mock<IReferralRepository>();
        Repository
            .Setup(r => r.AddAsync(It.IsAny<Referral>(), It.IsAny<IEnumerable<ReferralEvent>>(), It.IsAny<CancellationToken>()))
            .Callback((Referral referral, IEnumerable<ReferralEvent> events, CancellationToken ct) =>
            {
                Added.Add(referral);
                Saved.AddRange(events);
                Tokens.Add(ct);
            })
            .Returns(Task.CompletedTask);
        Repository
            .Setup(r => r.SaveAsync(It.IsAny<IEnumerable<ReferralEvent>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<ReferralEvent> events, CancellationToken ct) =>
            {
                Saved.AddRange(events);
                Tokens.Add(ct);
            })
            .Returns(Task.CompletedTask);
        Repository
            .Setup(r => r.GetOpenForPatientAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        Repository
            .Setup(r => r.GetHistoryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Saved.ToList());
    }

    [TearDown]
    public void TearDownReferralMocks() => Cts.Dispose();

    protected Referral GivenReferral(ReferralStatus status = ReferralStatus.AwaitingTriage, int version = 3)
    {
        var referral = CreateReferral(Patient, Specialty, status, version);
        Repository.Setup(r => r.GetForUpdateAsync(referral.Id, It.IsAny<CancellationToken>())).ReturnsAsync(referral);
        return referral;
    }
}

[TestFixture]
public sealed class RegisterReferralHandlerTests : ReferralCommandTestBase
{
    private RegisterReferralHandler Handler => new(Repository.Object, Patients.Object, Specialties.Object, Clock);

    private RegisterReferralCommand Command(bool confirm = false, string? ods = " z99 999 ") => new(
        Patient.Id, Specialty.Id, ReceivedOn, $"  {Practice} ", " ", ods, "high", $" {Reason} ", confirm, Registrar);

    [Test]
    public async Task Handle_Valid_AddsItAwaitingTriageWithItsRegisteredEntry()
    {
        var response = await Handler.Handle(Command(), Cts.Token);

        var referral = Added.Single();
        referral.Status.Should().Be(ReferralStatus.AwaitingTriage);
        referral.ReferrerPractice.Should().Be(Practice);
        referral.ReferrerClinician.Should().BeNull();
        referral.ReferrerOdsCode.Should().Be("Z99999");
        referral.Reason.Should().Be(Reason);
        referral.RequestedPriority.Should().Be(MedApp.Domain.Pathways.PatientPathways.PatientPathwayPriority.High);
        referral.RegisteredAtUtc.Should().Be(NowUtc);
        referral.RegisteredByUsername.Should().Be(Registrar.Username);
        referral.Version.Should().Be(1);
        Saved.Single().Action.Should().Be(ReferralEventAction.Registered);
        Tokens.Should().Equal(Cts.Token);
        response.Id.Should().Be(referral.Id);
        response.Patient!.Id.Should().Be(Patient.Id);
        response.SpecialtyName.Should().Be(Specialty.Name);
        response.History.Should().HaveCount(1);
    }

    [Test]
    public async Task Handle_ByAClinicianOfItsSpecialty_SaysTheyCanTriageIt()
    {
        Repository.Setup(r => r.IsSpecialtyMemberAsync(Triager.UserId, Specialty.Id, Cts.Token)).ReturnsAsync(true);

        var response = await Handler.Handle(Command() with { Actor = Triager }, Cts.Token);

        response.CanTriage.Should().BeTrue();
    }

    [Test]
    public async Task Handle_UnknownPatient_IsNotFound()
    {
        var act = () => Handler.Handle(Command() with { PatientId = Guid.NewGuid() }, Cts.Token);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_RestrictedRecord_IsAConflictSayingToGiveTheLetterToAnAdmin()
    {
        Patient.RestrictedAtUtc = NowUtc;

        var act = () => Handler.Handle(Command(), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(ReferralWorkflow.RestrictedRegisterMessage);
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_AnonymisedRecord_IsAConflict()
    {
        Patient.AnonymisedAtUtc = NowUtc;
        var act = () => Handler.Handle(Command(), Cts.Token);
        await act.Should().ThrowAsync<ConflictException>().WithMessage(ReferralWorkflow.AnonymisedRegisterMessage);
    }

    [Test]
    public async Task Handle_ArchivedSpecialty_IsAConflict()
    {
        Specialty.IsArchived = true;
        var act = () => Handler.Handle(Command(), Cts.Token);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Handle_OpenReferralToTheSameSpecialty_AsksForConfirmation()
    {
        var open = CreateReferral(Patient, Specialty);
        Repository.Setup(r => r.GetOpenForPatientAsync(Patient.Id, Specialty.Id, Cts.Token)).ReturnsAsync([open]);

        var act = () => Handler.Handle(Command(), Cts.Token);

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Single().PropertyName.Should().Be(nameof(RegisterReferralCommand.ConfirmPossibleDuplicate));
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_PossibleDuplicateConfirmed_RegistersItWithoutLooking()
    {
        await Handler.Handle(Command(confirm: true), Cts.Token);

        Added.Should().HaveCount(1);
        Repository.Verify(r => r.GetOpenForPatientAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

[TestFixture]
public sealed class EditReferralHandlerTests : ReferralCommandTestBase
{
    private EditReferralHandler Handler => new(Repository.Object, Specialties.Object, Clock);

    private static EditReferralCommand Command(Referral referral, Guid specialtyId, int version = 3, string reason = Canary) => new(
        referral.Id, specialtyId, referral.ReceivedOn, Practice, Clinician, OdsCode, "High", reason, version, Registrar);

    [Test]
    public async Task Handle_Changed_SavesTheEditWithItsEntry()
    {
        var referral = GivenReferral();

        var response = await Handler.Handle(Command(referral, Specialty.Id), Cts.Token);

        referral.Reason.Should().Be(Canary);
        referral.Version.Should().Be(4);
        Saved.Single().Action.Should().Be(ReferralEventAction.DetailsEdited);
        Saved.Single().Details.Should().NotContain(Canary);
        Tokens.Should().Equal(Cts.Token);
        response.Version.Should().Be(4);
    }

    [Test]
    public async Task Handle_NothingChanged_SavesNothing()
    {
        var referral = GivenReferral();

        await Handler.Handle(Command(referral, Specialty.Id, reason: Reason), Cts.Token);

        Repository.Verify(r => r.SaveAsync(It.IsAny<IEnumerable<ReferralEvent>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_ToAnotherSpecialty_ChecksItAndShowsIt()
    {
        var referral = GivenReferral();
        var other = SpecialtiesTestConstants.CreateSpecialty(SpecialtiesTestConstants.OtherSpecialtyName);
        Specialties = SpecialtiesTestConstants.RepositoryWith(Specialty, other);

        var response = await Handler.Handle(Command(referral, other.Id, reason: Reason), Cts.Token);

        response.SpecialtyName.Should().Be(other.Name);
        referral.SpecialtyId.Should().Be(other.Id);
    }

    [Test]
    public async Task Handle_ToAnArchivedSpecialty_IsAConflict()
    {
        var referral = GivenReferral();
        var archived = SpecialtiesTestConstants.CreateSpecialty(SpecialtiesTestConstants.OtherSpecialtyName, isArchived: true);
        Specialties = SpecialtiesTestConstants.RepositoryWith(Specialty, archived);

        var act = () => Handler.Handle(Command(referral, archived.Id), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Handle_StaleVersion_IsAConflict()
    {
        var referral = GivenReferral(version: 4);
        var act = () => Handler.Handle(Command(referral, Specialty.Id, version: 3), Cts.Token);
        await act.Should().ThrowAsync<ConflictException>().WithMessage(ReferralWorkflow.ChangedMeanwhileMessage);
    }

    [TestCase(ReferralStatus.Withdrawn, false)]
    [TestCase(ReferralStatus.AwaitingTriage, true)]
    public async Task Handle_ClosedOrRestricted_IsAConflict(ReferralStatus status, bool restricted)
    {
        var referral = GivenReferral(status);
        if (restricted)
            Patient.RestrictedAtUtc = NowUtc;

        var act = () => Handler.Handle(Command(referral, Specialty.Id), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>();
        Saved.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_Unknown_IsNotFound()
    {
        var act = () => Handler.Handle(Command(CreateReferral(), Specialty.Id), Cts.Token);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}

[TestFixture]
public sealed class CloseReferralHandlersTests : ReferralCommandTestBase
{
    private Task<MedApp.Contracts.Referrals.Responses.ReferralResponse> Close(bool withdraw, Guid id, int version, string detail = Detail) =>
        withdraw
            ? new WithdrawReferralHandler(Repository.Object, Clock).Handle(new WithdrawReferralCommand(id, $" {detail} ", version, Registrar), Cts.Token)
            : new MarkReferralEnteredInErrorHandler(Repository.Object, Clock).Handle(new MarkReferralEnteredInErrorCommand(id, $" {detail} ", version, Registrar), Cts.Token);

    [TestCase(true, ReferralStatus.Withdrawn)]
    [TestCase(false, ReferralStatus.EnteredInError)]
    public async Task Handle_AwaitingTriage_ClosesItWithItsEntry(bool withdraw, ReferralStatus status)
    {
        var referral = GivenReferral();

        var response = await Close(withdraw, referral.Id, 3);

        referral.Status.Should().Be(status);
        referral.DecisionDetail.Should().Be(Detail);
        Saved.Single().Action.Should().Be(withdraw ? ReferralEventAction.Withdrawn : ReferralEventAction.EnteredInError);
        Tokens.Should().Equal(Cts.Token);
        response.Status.Should().Be(status.ToString());
        response.IsOpen.Should().BeFalse();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Handle_RestrictedRecord_IsStillAllowed(bool withdraw)
    {
        var referral = GivenReferral();
        Patient.RestrictedAtUtc = NowUtc;

        await Close(withdraw, referral.Id, 3);

        referral.Status.Should().NotBe(ReferralStatus.AwaitingTriage);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Handle_AlreadyClosed_IsAConflict(bool withdraw)
    {
        var referral = GivenReferral(ReferralStatus.Withdrawn);
        var act = () => Close(withdraw, referral.Id, 3);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Handle_StaleVersion_IsAConflict(bool withdraw)
    {
        var referral = GivenReferral();
        var act = () => Close(withdraw, referral.Id, 2);
        await act.Should().ThrowAsync<ConflictException>().WithMessage(ReferralWorkflow.ChangedMeanwhileMessage);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Handle_AnonymisedRecord_IsAConflict(bool withdraw)
    {
        var referral = GivenReferral();
        Patient.AnonymisedAtUtc = NowUtc;
        var act = () => Close(withdraw, referral.Id, 3);
        await act.Should().ThrowAsync<ConflictException>();
    }
}

[TestFixture]
public sealed class ReferralValidatorsTests
{
    private readonly FixedTimeProvider _clock = new(NowUtc);

    private static RegisterReferralCommand Register(
        DateOnly? receivedOn = null, string practice = Practice, string? ods = OdsCode, string priority = "Urgent", string reason = Reason) =>
        new(Guid.NewGuid(), Guid.NewGuid(), receivedOn ?? ReceivedOn, practice, Clinician, ods, priority, reason, false, Registrar);

    [Test]
    public void Register_Valid_Passes() =>
        new RegisterReferralValidator(_clock).Validate(Register()).IsValid.Should().BeTrue();

    [Test]
    public void Register_ReceivedTodayOrAYearAgo_Passes()
    {
        var validator = new RegisterReferralValidator(_clock);
        validator.Validate(Register(Today)).IsValid.Should().BeTrue();
        validator.Validate(Register(Today.AddDays(-ReferralRules.MaxDaysBack))).IsValid.Should().BeTrue();
    }

    [Test]
    public void Register_ReceivedTomorrowOrOverAYearAgo_Fails()
    {
        var validator = new RegisterReferralValidator(_clock);
        validator.Validate(Register(Today.AddDays(1))).Errors.Single().ErrorMessage.Should().Be(ReferralRules.ReceivedInFutureMessage);
        validator.Validate(Register(Today.AddDays(-ReferralRules.MaxDaysBack - 1))).Errors.Single().ErrorMessage
            .Should().Be(ReferralRules.ReceivedTooLongAgoMessage);
    }

    [Test]
    public void Register_BadFields_Fail()
    {
        var validator = new RegisterReferralValidator(_clock);
        validator.Validate(Register(practice: " ")).IsValid.Should().BeFalse();
        validator.Validate(Register(practice: new string('a', Referral.MaxPracticeLength + 1))).IsValid.Should().BeFalse();
        validator.Validate(Register(ods: "A-1")).Errors.Single().ErrorMessage.Should().Be(ReferralRules.OdsCodeMessage);
        validator.Validate(Register(priority: "3")).IsValid.Should().BeFalse();
        validator.Validate(Register(reason: "")).IsValid.Should().BeFalse();
        validator.Validate(Register(reason: new string('a', Referral.MaxReasonLength + 1))).IsValid.Should().BeFalse();
        validator.Validate(Register() with { PatientId = Guid.Empty, SpecialtyId = Guid.Empty }).Errors.Should().HaveCount(2);
    }

    [Test]
    public void Edit_NeedsAVersion()
    {
        var validator = new EditReferralValidator(_clock);
        var command = new EditReferralCommand(Guid.NewGuid(), Guid.NewGuid(), ReceivedOn, Practice, null, null, "Low", Reason, 1, Registrar);

        validator.Validate(command).IsValid.Should().BeTrue();
        validator.Validate(command with { ExpectedVersion = 0 }).IsValid.Should().BeFalse();
    }

    [Test]
    public void Close_NeedsADetailOfAtMost500Characters()
    {
        new WithdrawReferralValidator().Validate(new WithdrawReferralCommand(Guid.NewGuid(), Detail, 1, Registrar)).IsValid.Should().BeTrue();
        new WithdrawReferralValidator().Validate(new WithdrawReferralCommand(Guid.NewGuid(), " ", 1, Registrar))
            .Errors.Single().ErrorMessage.Should().Be(ReferralRules.DetailRequiredMessage);
        new MarkReferralEnteredInErrorValidator()
            .Validate(new MarkReferralEnteredInErrorCommand(Guid.NewGuid(), new string('a', Referral.MaxDetailLength + 1), 1, Registrar))
            .IsValid.Should().BeFalse();
        new MarkReferralEnteredInErrorValidator()
            .Validate(new MarkReferralEnteredInErrorCommand(Guid.Empty, Detail, 0, Registrar))
            .Errors.Should().HaveCount(2);
    }
}
