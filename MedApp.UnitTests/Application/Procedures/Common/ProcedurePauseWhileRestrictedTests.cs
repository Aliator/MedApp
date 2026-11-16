using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.PatientProcedures.Commands.ChangePatientProcedureStatus;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.UnitTests.Application.Procedures.Common;

/// <summary>
/// While a patient's record is restricted, its procedures can still be paused (blocked or cancelled, with a reason),
/// so care under way can be stopped safely; nothing else changes. An anonymised record's can't even be paused. The
/// procedure's response carries the patient's flags for its banners.
/// </summary>
[TestFixture]
public sealed class ProcedurePauseWhileRestrictedTests : ProcedureHandlerTestBase
{
    private Task<MedApp.Contracts.Procedures.PatientProcedures.Responses.PatientProcedureResponse> ChangeStatus(PatientProcedure procedure, string to, string? reason) =>
        Fixture.Create<ChangePatientProcedureStatusHandler>().Handle(
            new ChangePatientProcedureStatusCommand(procedure.Id, to, reason, Staff), CancellationToken.None);

    [TestCase("Blocked")]
    [TestCase("Cancelled")]
    public async Task ChangeStatus_RestrictedPatient_PausingIsAllowed(string to)
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress, patient: PrivacyTestConstants.CreateRestrictedPatient()));

        var response = await ChangeStatus(procedure, to, Reason);

        procedure.Status.ToString().Should().Be(to);
        response.Status.Should().Be(to);
        SavedAudit.Should().ContainSingle(e => e.Action == PatientProcedureAuditAction.StatusChanged);
        VerifySavedOnce();
    }

    [Test]
    public async Task ChangeStatus_RestrictedPatient_UnblockingIsRefused()
    {
        var procedure = CreateProcedure(Status.Blocked, patient: PrivacyTestConstants.CreateRestrictedPatient());
        procedure.BlockedFromStatus = Status.InProgress;
        GivenProcedure(procedure);

        var act = () => ChangeStatus(procedure, "InProgress", null);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.RestrictedMessage);
        procedure.Status.Should().Be(Status.Blocked);
        VerifyNotSaved();
    }

    [Test]
    public async Task ChangeStatus_RestrictedPatient_CompletingIsRefused()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InReview, finishedStages: 3, patient: PrivacyTestConstants.CreateRestrictedPatient()));

        var act = () => ChangeStatus(procedure, "Completed", null);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.RestrictedMessage);
        VerifyNotSaved();
    }

    [TestCase("Blocked")]
    [TestCase("Cancelled")]
    public async Task ChangeStatus_AnonymisedPatient_EvenPausingIsRefused(string to)
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress, patient: PrivacyTestConstants.CreateAnonymisedPatient()));

        var act = () => ChangeStatus(procedure, to, Reason);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.AnonymisedMessage);
        VerifyNotSaved();
    }

    [Test]
    public async Task GetForPauseOrThrowAsync_UnknownProcedure_IsNotFound()
    {
        var act = () => Repository.Object.GetForPauseOrThrowAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task GetForPauseOrThrowAsync_RestrictedPatient_ReturnsTheProcedure_AndPassesTheToken()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress, patient: PrivacyTestConstants.CreateRestrictedPatient()));
        using var cts = new CancellationTokenSource();

        (await Repository.Object.GetForPauseOrThrowAsync(procedure.Id, cts.Token)).Should().BeSameAs(procedure);
        Repository.Verify(r => r.GetForUpdateAsync(procedure.Id, cts.Token), Times.Once);
    }

    [Test]
    public void ToResponse_RestrictedPatient_OffersOnlyPausing_AndCarriesTheFlags()
    {
        var patient = PrivacyTestConstants.CreateRestrictedPatient();
        patient.DisputedAtUtc = PrivacyTestConstants.FlaggedAtUtc;
        patient.DisputeReason = "Disputes the recorded allergy.";
        var procedure = CreateProcedure(Status.InProgress, finishedStages: 1, patient: patient);

        var response = ProcedureMapper.ToResponse(procedure, Usernames, Now);

        response.AllowedStatusChanges.Should().BeEquivalentTo("Blocked", "Cancelled");
        response.CanWorkOnStages.Should().BeFalse();
        response.PatientFlags!.IsRestricted.Should().BeTrue();
        response.PatientFlags.RestrictionReason.Should().Be(PrivacyTestConstants.Reason);
        response.PatientFlags.IsDisputed.Should().BeTrue();
        response.PatientFlags.DisputeReason.Should().Be("Disputes the recorded allergy.");
        response.PatientFlags.IsAnonymised.Should().BeFalse();
    }

    [Test]
    public void ToResponse_OrdinaryPatient_HasFlagsAllClear()
    {
        var response = ProcedureMapper.ToResponse(CreateProcedure(Status.InProgress), Usernames, Now);

        response.PatientFlags.Should().BeEquivalentTo(new { IsRestricted = false, IsDisputed = false, IsAnonymised = false });
    }

    [Test]
    public void ToResponse_NoPatientLoaded_HasNoFlags()
    {
        var procedure = CreateProcedure(Status.InProgress);
        procedure.Patient = null!;

        ProcedureMapper.ToResponse(procedure, Usernames, Now, "Named elsewhere").PatientFlags.Should().BeNull();
    }

    [Test]
    public void PausesWhileRestricted_AreBlockingAndCancelling()
    {
        PatientRules.PausesWhileRestricted.Should().BeEquivalentTo([Status.Blocked, Status.Cancelled]);
        PatientRules.RestrictedMessage.Should().Contain("blocked or cancelled");
    }

    [Test]
    public void EnsureCanPause_RefusesOnlyAnAnonymisedRecord()
    {
        var act = () => PatientRules.EnsureCanPause(PrivacyTestConstants.CreateAnonymisedPatient());

        act.Should().Throw<ConflictException>().WithMessage(PatientRules.AnonymisedMessage);
        FluentActions.Invoking(() => PatientRules.EnsureCanPause(PrivacyTestConstants.CreateRestrictedPatient())).Should().NotThrow();
        FluentActions.Invoking(() => PatientRules.EnsureCanPause(null)).Should().NotThrow();
    }
}
