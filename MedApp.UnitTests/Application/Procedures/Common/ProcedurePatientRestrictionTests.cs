using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.PatientProcedures.Commands.CreatePatientProcedure;
using MedApp.Application.Procedures.PatientProcedures.Commands.DeletePatientProcedure;
using MedApp.Application.Procedures.PatientProcedures.Commands.UpdateProcedureStageNotes;
using MedApp.Application.Procedures.PatientProcedures.Queries.GetPatientProcedureById;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Patients;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.UnitTests.Application.Procedures.Common;

/// <summary>
/// A restricted or anonymised patient's procedures can't be changed: the mapper offers nothing, and every change
/// (loaded through <c>GetForUpdateOrThrowAsync</c>) is refused. Opening one is recorded in the activity log.
/// </summary>
[TestFixture]
public sealed class ProcedurePatientRestrictionTests : ProcedureHandlerTestBase
{
    private static IEnumerable<Func<Patient>> LockedPatients()
    {
        yield return PrivacyTestConstants.CreateRestrictedPatient;
        yield return PrivacyTestConstants.CreateAnonymisedPatient;
    }

    [TestCaseSource(nameof(LockedPatients))]
    public void ToResponse_LockedPatient_OffersNothingButPausing(Func<Patient> patient)
    {
        var procedure = CreateProcedure(Status.InReview, finishedStages: 3, patient: patient());
        procedure.Stages[2].IsLocked = true;

        var response = ProcedureMapper.ToResponse(procedure, Usernames, Now);

        response.ReadOnlyReason.Should().Be(PatientRules.WhyCantChange(procedure.Patient));
        // A restricted record's procedures can still be blocked or cancelled; an anonymised one's, nothing.
        response.AllowedStatusChanges.Should().BeEquivalentTo(
            PatientRules.IsAnonymised(procedure.Patient) ? [] : new[] { "Blocked", "Cancelled" });
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
        var response = ProcedureMapper.ToResponse(CreateProcedure(Status.InReview, finishedStages: 3), Usernames, Now);

        response.ReadOnlyReason.Should().BeNull();
        response.AllowedStatusChanges.Should().NotBeEmpty();
        response.Stages.Should().Contain(s => s.CanUnlock);
    }

    [Test]
    public void ToResponse_DeletableProcedureOfAnOrdinaryPatient_CanBeDeleted()
    {
        ProcedureMapper.ToResponse(CreateProcedure(Status.Cancelled), Usernames, Now).CanBeDeleted.Should().BeTrue();
    }

    [Test]
    public async Task UpdateStageNotes_RestrictedPatient_IsRefused()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress, patient: PrivacyTestConstants.CreateRestrictedPatient()));

        var act = () => Fixture.Create<UpdateProcedureStageNotesHandler>().Handle(
            new UpdateProcedureStageNotesCommand(procedure.Id, procedure.StageAt(1).Id, "Changed", Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.RestrictedMessage);
        procedure.StageAt(1).Notes.Should().BeEmpty();
        VerifyNotSaved();
    }

    [Test]
    public async Task DeleteProcedure_AnonymisedPatient_IsRefused()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.Cancelled, patient: PrivacyTestConstants.CreateAnonymisedPatient()));

        var act = () => Fixture.Create<DeletePatientProcedureHandler>().Handle(
            new DeletePatientProcedureCommand(procedure.Id, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.AnonymisedMessage);
    }

    [TestCaseSource(nameof(LockedPatients))]
    public async Task CreateProcedure_LockedPatient_IsRefused(Func<Patient> makePatient)
    {
        var patient = makePatient();
        var patients = new Mock<IPatientRepository>();
        patients.Setup(r => r.GetByIdAsync(patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        var stages = new Mock<IPatientProcedureStagesRepository>();
        Fixture.Inject(patients.Object);
        Fixture.Inject(stages.Object);

        var act = () => Fixture.Create<CreatePatientProcedureHandler>().Handle(
            new CreatePatientProcedureCommand(patient.Id, Guid.NewGuid(), null, "", Today.AddDays(3), "high", Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        stages.Verify(s => s.GetStageTemplateByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        Repository.Verify(r => r.AddAsync(It.IsAny<PatientProcedure>(), It.IsAny<IEnumerable<PatientProcedureAuditEntry>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetById_RecordsAProcedureView_AgainstItsPatient()
    {
        var activity = new RecordingActivityLog();
        Fixture.Inject<IActivityLog>(activity);
        var procedure = GivenProcedure(CreateProcedure());
        using var cts = new CancellationTokenSource();

        await Fixture.Create<GetPatientProcedureByIdHandler>().Handle(new GetPatientProcedureByIdQuery(procedure.Id), cts.Token);

        activity.Views.Should().ContainSingle().Which.Should().BeEquivalentTo(
            (ActivityAction.ProcedureViewed, new List<Guid> { procedure.PatientId }, (Guid?)procedure.Id));
        activity.Tokens.Should().Equal(cts.Token);
    }

    [Test]
    public async Task GetById_NotFound_RecordsNothing()
    {
        var activity = new RecordingActivityLog();
        Fixture.Inject<IActivityLog>(activity);

        var act = () => Fixture.Create<GetPatientProcedureByIdHandler>().Handle(new GetPatientProcedureByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        activity.Views.Should().BeEmpty();
    }
}
