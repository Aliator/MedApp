using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.PatientPathways.Commands.CreatePatientPathway;
using MedApp.Application.Pathways.PatientPathways.Commands.DeletePatientPathway;
using MedApp.Application.Pathways.PatientPathways.Commands.UpdatePathwayStageNotes;
using MedApp.Application.Pathways.PatientPathways.Queries.GetPatientPathwayById;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Patients;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Pathways.Common;

/// <summary>
/// A restricted or anonymised patient's pathways can't be changed: the mapper offers nothing, and every change
/// (loaded through <c>GetForUpdateOrThrowAsync</c>) is refused. Opening one is recorded in the activity log.
/// </summary>
[TestFixture]
public sealed class PathwayPatientRestrictionTests : PathwayHandlerTestBase
{
    private static IEnumerable<Func<Patient>> LockedPatients()
    {
        yield return PrivacyTestConstants.CreateRestrictedPatient;
        yield return PrivacyTestConstants.CreateAnonymisedPatient;
    }

    [TestCaseSource(nameof(LockedPatients))]
    public void ToResponse_LockedPatient_OffersNothingButPausing(Func<Patient> patient)
    {
        var pathway = CreatePathway(Status.InReview, finishedStages: 3, patient: patient());
        pathway.Stages[2].IsLocked = true;

        var response = PathwayMapper.ToResponse(pathway, Usernames, Now);

        response.ReadOnlyReason.Should().Be(PatientRules.WhyCantChange(pathway.Patient));
        // A restricted record's pathways can still be blocked or cancelled; an anonymised one's, nothing.
        response.AllowedStatusChanges.Should().BeEquivalentTo(
            PatientRules.IsAnonymised(pathway.Patient) ? [] : new[] { "Blocked", "Cancelled" });
        response.CanWorkOnStages.Should().BeFalse();
        response.CanEditDetails.Should().BeFalse();
        response.CanChangeAssignments.Should().BeFalse();
        response.CanBeDeleted.Should().BeFalse();
        response.Stages.Should().AllSatisfy(s =>
        {
            s.CanEdit.Should().BeFalse();
            s.CanUnlock.Should().BeFalse();
        });
    }

    [Test]
    public void ToResponse_OrdinaryPatient_HasNoReadOnlyReason()
    {
        var response = PathwayMapper.ToResponse(CreatePathway(Status.InReview, finishedStages: 3), Usernames, Now);

        response.ReadOnlyReason.Should().BeNull();
        response.AllowedStatusChanges.Should().NotBeEmpty();
        response.Stages.Should().Contain(s => s.CanUnlock);
    }

    [Test]
    public void ToResponse_DeletablePathwayOfAnOrdinaryPatient_CanBeDeleted()
    {
        PathwayMapper.ToResponse(CreatePathway(Status.Cancelled), Usernames, Now).CanBeDeleted.Should().BeTrue();
    }

    [Test]
    public async Task UpdateStageNotes_RestrictedPatient_IsRefused()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress, patient: PrivacyTestConstants.CreateRestrictedPatient()));

        var act = () => Fixture.Create<UpdatePathwayStageNotesHandler>().Handle(
            new UpdatePathwayStageNotesCommand(pathway.Id, pathway.StageAt(1).Id, "Changed", Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.RestrictedMessage);
        pathway.StageAt(1).Notes.Should().BeEmpty();
        VerifyNotSaved();
    }

    [Test]
    public async Task DeletePathway_AnonymisedPatient_IsRefused()
    {
        var pathway = GivenPathway(CreatePathway(Status.Cancelled, patient: PrivacyTestConstants.CreateAnonymisedPatient()));

        var act = () => Fixture.Create<DeletePatientPathwayHandler>().Handle(
            new DeletePatientPathwayCommand(pathway.Id, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.AnonymisedMessage);
    }

    [TestCaseSource(nameof(LockedPatients))]
    public async Task CreatePathway_LockedPatient_IsRefused(Func<Patient> makePatient)
    {
        var patient = makePatient();
        var patients = new Mock<IPatientRepository>();
        patients.Setup(r => r.GetByIdAsync(patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        var stages = new Mock<IPatientPathwayStagesRepository>();
        Fixture.Inject(patients.Object);
        Fixture.Inject(stages.Object);

        var act = () => Fixture.Create<CreatePatientPathwayHandler>().Handle(
            new CreatePatientPathwayCommand(patient.Id, Guid.NewGuid(), null, "", Today.AddDays(3), "high", Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        stages.Verify(s => s.GetPathwayTemplateByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        Repository.Verify(r => r.AddAsync(It.IsAny<PatientPathway>(), It.IsAny<IEnumerable<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetById_RecordsAPathwayView_AgainstItsPatient()
    {
        var activity = new RecordingActivityLog();
        Fixture.Inject<IActivityLog>(activity);
        var pathway = GivenPathway(CreatePathway());
        using var cts = new CancellationTokenSource();

        await Fixture.Create<GetPatientPathwayByIdHandler>().Handle(new GetPatientPathwayByIdQuery(pathway.Id), cts.Token);

        activity.Views.Should().ContainSingle().Which.Should().BeEquivalentTo(
            (ActivityAction.PathwayViewed, new List<Guid> { pathway.PatientId }, (Guid?)pathway.Id));
        activity.Tokens.Should().Equal(cts.Token);
    }

    [Test]
    public async Task GetById_NotFound_RecordsNothing()
    {
        var activity = new RecordingActivityLog();
        Fixture.Inject<IActivityLog>(activity);

        var act = () => Fixture.Create<GetPatientPathwayByIdHandler>().Handle(new GetPatientPathwayByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        activity.Views.Should().BeEmpty();
    }
}
