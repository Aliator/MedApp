using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Commands.SetPatientFlag;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Patients;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Patients.Commands.SetPatientFlag;

[TestFixture]
public sealed class SetPatientFlagHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IPatientRepository> _repository = null!;
    private ActivityEntry? _entry;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _repository = new Mock<IPatientRepository>();
        _entry = null;
        _repository
            .Setup(r => r.UpdateAsync(It.IsAny<Patient>(), It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()))
            .Callback((Patient _, ActivityEntry e, CancellationToken _) => _entry = e)
            .Returns(Task.CompletedTask);
        _fixture.Inject(_repository.Object);
        _fixture.Inject<IActivityLog>(new RecordingActivityLog());
        _fixture.Inject<TimeProvider>(new FixedTimeProvider(ActivityTestConstants.Now));
    }

    private SetPatientFlagHandler Handler => _fixture.Create<SetPatientFlagHandler>();

    private Patient Given(Patient patient)
    {
        _repository.Setup(r => r.GetByIdAsync(patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        return patient;
    }

    [Test]
    public async Task Handle_UnknownPatient_ThrowsNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Patient?)null);

        var act = () => Handler.Handle(new SetPatientFlagCommand(Guid.NewGuid(), PatientFlag.Restricted, true, "r"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_AnonymisedRecord_ThrowsConflict()
    {
        var patient = Given(PrivacyTestConstants.CreateAnonymisedPatient());

        var act = () => Handler.Handle(new SetPatientFlagCommand(patient.Id, PatientFlag.Disputed, true, "r"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.AnonymisedMessage);
    }

    [Test]
    public async Task Handle_Restrict_SetsTheFlagNow_WithTheTrimmedReason_AndRecordsIt()
    {
        var patient = Given(PatientsTestConstants.CreateValidPatient());
        using var cts = new CancellationTokenSource();

        var response = await Handler.Handle(new SetPatientFlagCommand(patient.Id, PatientFlag.Restricted, true, "  Synthetic reason  "), cts.Token);

        patient.RestrictedAtUtc.Should().Be(ActivityTestConstants.Now);
        patient.RestrictionReason.Should().Be("Synthetic reason");
        response.IsRestricted.Should().BeTrue();
        _entry!.Action.Should().Be(ActivityAction.PatientRestricted);
        _entry.PatientId.Should().Be(patient.Id);
        _entry.Details.Should().Be("""{"reason":"Synthetic reason"}""");
        _repository.Verify(r => r.UpdateAsync(patient, It.IsAny<ActivityEntry>(), cts.Token), Times.Once);
    }

    [Test]
    public async Task Handle_Dispute_SetsTheFlag_AndRecordsIt()
    {
        var patient = Given(PatientsTestConstants.CreateValidPatient());

        var response = await Handler.Handle(new SetPatientFlagCommand(patient.Id, PatientFlag.Disputed, true, "Disputes a note"), CancellationToken.None);

        patient.DisputedAtUtc.Should().Be(ActivityTestConstants.Now);
        patient.DisputeReason.Should().Be("Disputes a note");
        response.IsDisputed.Should().BeTrue();
        _entry!.Action.Should().Be(ActivityAction.PatientDisputed);
    }

    [Test]
    public async Task Handle_SetAgain_ReplacesTheReason()
    {
        var patient = Given(PrivacyTestConstants.CreateRestrictedPatient());

        await Handler.Handle(new SetPatientFlagCommand(patient.Id, PatientFlag.Restricted, true, "A new reason"), CancellationToken.None);

        patient.RestrictionReason.Should().Be("A new reason");
        _entry!.Action.Should().Be(ActivityAction.PatientRestricted);
    }

    [Test]
    public async Task Handle_LiftRestriction_ClearsIt_AndRecordsIt()
    {
        var patient = Given(PrivacyTestConstants.CreateRestrictedPatient());

        var response = await Handler.Handle(new SetPatientFlagCommand(patient.Id, PatientFlag.Restricted, false, null), CancellationToken.None);

        patient.RestrictedAtUtc.Should().BeNull();
        patient.RestrictionReason.Should().BeNull();
        response.IsRestricted.Should().BeFalse();
        _entry!.Action.Should().Be(ActivityAction.PatientRestrictionLifted);
        _entry.Details.Should().Be("{}");
    }

    [Test]
    public async Task Handle_ClearDispute_ClearsIt_AndRecordsTheReason()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.DisputedAtUtc = PrivacyTestConstants.FlaggedAtUtc;
        patient.DisputeReason = "Old";
        Given(patient);

        await Handler.Handle(new SetPatientFlagCommand(patient.Id, PatientFlag.Disputed, false, "Resolved"), CancellationToken.None);

        patient.DisputedAtUtc.Should().BeNull();
        patient.DisputeReason.Should().BeNull();
        _entry!.Action.Should().Be(ActivityAction.PatientDisputeCleared);
        _entry.Details.Should().Be("""{"reason":"Resolved"}""");
    }

    [TestCase(PatientFlag.Restricted)]
    [TestCase(PatientFlag.Disputed)]
    public async Task Handle_ClearAFlagThatIsntSet_ChangesNothing(PatientFlag flag)
    {
        var patient = Given(PatientsTestConstants.CreateValidPatient());

        var response = await Handler.Handle(new SetPatientFlagCommand(patient.Id, flag, false, null), CancellationToken.None);

        response.Id.Should().Be(patient.Id);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Patient>(), It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
