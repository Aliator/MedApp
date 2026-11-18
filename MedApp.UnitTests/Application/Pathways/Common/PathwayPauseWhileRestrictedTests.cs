using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.PatientPathways.Commands.ChangePatientPathwayStatus;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Pathways.Common;

/// <summary>
/// While a patient's record is restricted, its pathways can still be paused (blocked or cancelled, with a reason),
/// so care under way can be stopped safely; nothing else changes. An anonymised record's can't even be paused. The
/// pathway's response carries the patient's flags for its banners.
/// </summary>
[TestFixture]
public sealed class PathwayPauseWhileRestrictedTests : PathwayHandlerTestBase
{
    private Task<MedApp.Contracts.Pathways.PatientPathways.Responses.PatientPathwayResponse> ChangeStatus(PatientPathway pathway, string to, string? reason) =>
        Fixture.Create<ChangePatientPathwayStatusHandler>().Handle(
            new ChangePatientPathwayStatusCommand(pathway.Id, to, reason, Staff), CancellationToken.None);

    [TestCase("Blocked")]
    [TestCase("Cancelled")]
    public async Task ChangeStatus_RestrictedPatient_PausingIsAllowed(string to)
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress, patient: PrivacyTestConstants.CreateRestrictedPatient()));

        var response = await ChangeStatus(pathway, to, Reason);

        pathway.Status.ToString().Should().Be(to);
        response.Status.Should().Be(to);
        SavedAudit.Should().ContainSingle(e => e.Action == PatientPathwayAuditAction.StatusChanged);
        VerifySavedOnce();
    }

    [Test]
    public async Task ChangeStatus_RestrictedPatient_UnblockingIsRefused()
    {
        var pathway = CreatePathway(Status.Blocked, patient: PrivacyTestConstants.CreateRestrictedPatient());
        pathway.BlockedFromStatus = Status.InProgress;
        GivenPathway(pathway);

        var act = () => ChangeStatus(pathway, "InProgress", null);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.RestrictedMessage);
        pathway.Status.Should().Be(Status.Blocked);
        VerifyNotSaved();
    }

    [Test]
    public async Task ChangeStatus_RestrictedPatient_CompletingIsRefused()
    {
        var pathway = GivenPathway(CreatePathway(Status.InReview, finishedStages: 3, patient: PrivacyTestConstants.CreateRestrictedPatient()));

        var act = () => ChangeStatus(pathway, "Completed", null);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.RestrictedMessage);
        VerifyNotSaved();
    }

    [TestCase("Blocked")]
    [TestCase("Cancelled")]
    public async Task ChangeStatus_AnonymisedPatient_EvenPausingIsRefused(string to)
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress, patient: PrivacyTestConstants.CreateAnonymisedPatient()));

        var act = () => ChangeStatus(pathway, to, Reason);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.AnonymisedMessage);
        VerifyNotSaved();
    }

    [Test]
    public async Task GetForPauseOrThrowAsync_UnknownPathway_IsNotFound()
    {
        var act = () => Repository.Object.GetForPauseOrThrowAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task GetForPauseOrThrowAsync_RestrictedPatient_ReturnsThePathway_AndPassesTheToken()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress, patient: PrivacyTestConstants.CreateRestrictedPatient()));
        using var cts = new CancellationTokenSource();

        (await Repository.Object.GetForPauseOrThrowAsync(pathway.Id, cts.Token)).Should().BeSameAs(pathway);
        Repository.Verify(r => r.GetForUpdateAsync(pathway.Id, cts.Token), Times.Once);
    }

    [Test]
    public void ToResponse_RestrictedPatient_OffersOnlyPausing_AndCarriesTheFlags()
    {
        var patient = PrivacyTestConstants.CreateRestrictedPatient();
        patient.DisputedAtUtc = PrivacyTestConstants.FlaggedAtUtc;
        patient.DisputeReason = "Disputes the recorded allergy.";
        var pathway = CreatePathway(Status.InProgress, finishedStages: 1, patient: patient);

        var response = PathwayMapper.ToResponse(pathway, Usernames, Now);

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
        var response = PathwayMapper.ToResponse(CreatePathway(Status.InProgress), Usernames, Now);

        response.PatientFlags.Should().BeEquivalentTo(new { IsRestricted = false, IsDisputed = false, IsAnonymised = false });
    }

    [Test]
    public void ToResponse_NoPatientLoaded_HasNoFlags()
    {
        var pathway = CreatePathway(Status.InProgress);
        pathway.Patient = null!;

        PathwayMapper.ToResponse(pathway, Usernames, Now, "Named elsewhere").PatientFlags.Should().BeNull();
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
