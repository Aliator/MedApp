using AutoFixture;
using FluentAssertions;
using FluentValidation;
using MedApp.Application.Activity.Common;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Attachments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Commands.AnonymisePatient;
using MedApp.Application.Patients.Commands.DeletePatient;
using MedApp.Application.Patients.Commands.SetPatientFlag;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Specialties.Commands.SetSpecialtyArchived;
using MedApp.Application.Specialties.Common;
using MedApp.Domain.Attachments;
using MedApp.Domain.Referrals;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ReferralsTestConstants;

namespace MedApp.UnitTests.Application.Referrals;

/// <summary>Referrals in the patient's record: export, anonymisation, deletion, restriction, retention and files.</summary>
[TestFixture]
public sealed class ReferralPrivacyTests
{
    private static (PatientRecord Record, Referral Referral, ReferralEvent Registered) RecordWithReferral(
        ReferralStatus status = ReferralStatus.Withdrawn)
    {
        var record = PrivacyTestConstants.CreateRecord();
        var referral = CreateReferral(record.Patient, status: status);
        referral.Reason = Canary;
        referral.ReferrerPractice = Canary;
        referral.ReferrerClinician = Canary;
        referral.DecisionDetail = Canary;
        referral.RedirectedTo = Canary;
        referral.RedirectDetail = Canary;
        var change = new ReferralChange(referral, Registrar, NowUtc);
        change.Register(referral.Specialty);
        // A finished pause and an open one, each with the canary as its detail.
        var clock = new ReferralChange(referral, Registrar, NowUtc);
        clock.ResumeClock(clock.PauseClock(ClockPauseReason.Other, Canary, Today.AddDays(10)));
        new ReferralChange(referral, Registrar, NowUtc.AddMinutes(5))
            .PauseClock(ClockPauseReason.PatientChoseToWait, Canary, Today.AddDays(20));
        var letter = new Attachment
        {
            Id = Guid.NewGuid(), PatientId = record.Patient.Id, ReferralId = referral.Id, FileName = Canary, ContentType = "application/pdf"
        };
        return (record with { Referrals = [referral], ReferralHistory = change.Events, Attachments = [letter] }, referral, change.Events[0]);
    }

    [Test]
    public void Export_ListsEveryReferralFieldAndItsHistoryWithoutStaffIds()
    {
        var (record, referral, _) = RecordWithReferral();

        var export = PatientExportMapper.ToResponse(record, new Dictionary<Guid, string>(), NowUtc, "admin");

        var exported = export.Referrals.Single();
        exported.Id.Should().Be(referral.Id);
        exported.Reason.Should().Be(Canary);
        exported.ReferrerPractice.Should().Be(Canary);
        exported.ReferrerOdsCode.Should().Be(OdsCode);
        exported.SpecialtyName.Should().Be(SpecialtiesTestConstants.SpecialtyName);
        exported.Status.Should().Be("Withdrawn");
        exported.RegisteredBy.Should().Be(Registrar.Username);
        exported.DecisionDetail.Should().Be(Canary);
        exported.RedirectDetail.Should().Be(Canary);
        exported.TargetDays.Should().Be(28);
        exported.WaitStartOn.Should().Be(ReceivedOn);
        exported.TargetOn.Should().Be(ReceivedOn.AddDays(28));
        exported.Pauses.Should().HaveCount(2);
        exported.Pauses[0].Should().BeEquivalentTo(new
        {
            PausedOn = Today, PausedBy = Registrar.Username, Reason = "Other", Detail = Canary, ReviewOn = Today.AddDays(10),
            ResumedOn = (DateOnly?)Today, ResumedBy = Registrar.Username
        });
        exported.Pauses[1].ResumedOn.Should().BeNull();
        var history = export.ReferralHistory.Single();
        history.Action.Should().Be("Registered");
        history.Username.Should().Be(Registrar.Username);
        history.Details.Should().NotContain(Registrar.UserId.ToString());
        export.Attachments.Single().AttachedTo.Should().Be("Referral");
        export.Attachments.Single().ReferralId.Should().Be(referral.Id);
    }

    [Test]
    public void Anonymise_ReplacesTheReferralsFreeTextAndKeepsItsStructure()
    {
        var (record, referral, registered) = RecordWithReferral();

        var deletions = PatientAnonymiser.Apply(record, NowUtc, ActivityTestConstants.Actor);

        referral.Reason.Should().Be(PatientAnonymiser.RemovedText);
        referral.ReferrerPractice.Should().Be(PatientAnonymiser.RemovedText);
        referral.ReferrerClinician.Should().Be(PatientAnonymiser.RemovedText);
        referral.DecisionDetail.Should().Be(PatientAnonymiser.RemovedText);
        referral.RedirectedTo.Should().Be(PatientAnonymiser.RemovedText);
        referral.RedirectDetail.Should().Be(PatientAnonymiser.RemovedText);
        referral.ReferrerOdsCode.Should().BeNull();
        referral.Pauses.Should().OnlyContain(p => p.Detail == PatientAnonymiser.RemovedText);
        referral.Pauses.Select(p => p.Reason).Should().Equal(ClockPauseReason.Other, ClockPauseReason.PatientChoseToWait);
        referral.ReceivedOn.Should().Be(ReceivedOn);
        referral.Status.Should().Be(ReferralStatus.Withdrawn);
        registered.Details.Should().Contain(SpecialtiesTestConstants.SpecialtyName);
        // The letter is renamed and deleted like any other file, recorded as attached to the referral.
        record.Attachments.Single().FileName.Should().StartWith(PatientAnonymiser.FileName);
        record.Attachments.Single().IsDeleted.Should().BeTrue();
        deletions.Single().Details.Should().Contain("\"attachedTo\":\"Referral\"").And.NotContain(Canary);
    }

    [Test]
    public async Task Anonymise_WhileAReferralAwaitsTriage_IsAConflict()
    {
        var (record, _, _) = RecordWithReferral(ReferralStatus.AwaitingTriage);
        record = record with { Pathways = [], Appointments = [] };
        var repository = new Mock<IPatientRecordRepository>();
        repository.Setup(r => r.GetAsync(record.Patient.Id, true, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        var handler = new AnonymisePatientHandler(repository.Object, new RecordingActivityLog(), new FixedTimeProvider(NowUtc));

        var act = () => handler.Handle(
            new AnonymisePatientCommand(record.Patient.Id, "RetentionPeriodEnded", null, PatientMapper.FullName(record.Patient)),
            CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Message.Should().Contain("awaiting triage");
    }

    [Test]
    public async Task DeletePatient_WithReferrals_IsAConflict()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        var patients = new Mock<IPatientRepository>();
        patients.Setup(p => p.GetByIdAsync(patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        patients.Setup(p => p.CountReferralsAsync(patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(2);
        var handler = new DeletePatientHandler(patients.Object, new RecordingActivityLog(), new Mock<IAttachmentRepository>().Object);

        var act = () => handler.Handle(new DeletePatientCommand(patient.Id), CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Message.Should().Contain("2 referrals");
        patients.Verify(p => p.DeleteAsync(It.IsAny<Guid>(), It.IsAny<MedApp.Domain.Activity.ActivityEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase(0, 0, 1, "1 referral awaiting triage")]
    [TestCase(1, 0, 3, "3 referrals awaiting triage")]
    public async Task Restrict_WithReferralsAwaitingTriage_AsksForConfirmationCountingThem(
        int underWay, int appointments, int awaiting, string expected)
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        var patients = new Mock<IPatientRepository>();
        patients.Setup(p => p.GetByIdAsync(patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        patients.Setup(p => p.CountPathwaysUnderWayAsync(patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(underWay);
        patients.Setup(p => p.CountOpenAppointmentsAsync(patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(appointments);
        patients.Setup(p => p.CountReferralsAwaitingTriageAsync(patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(awaiting);
        var handler = new SetPatientFlagHandler(patients.Object, new RecordingActivityLog(), new FixedTimeProvider(NowUtc));

        var act = () => handler.Handle(
            new SetPatientFlagCommand(patient.Id, PatientFlag.Restricted, true, "Synthetic reason", false), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Single().ErrorMessage
            .Should().Contain(expected).And.Contain("withdrawing");
    }

    [Test]
    public void Retention_AReferralChangeLaterThanTheRest_IsTheConclusionOfTreatment()
    {
        var candidate = PrivacyTestConstants.CreateCandidate(new DateOnly(1970, 1, 1), new DateTime(2010, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            with { LastReferralChangeUtc = new DateTime(2015, 6, 1, 12, 0, 0, DateTimeKind.Utc) };

        RetentionSchedule.Due(candidate, new RetentionOptions()).ConclusionOfTreatment.Should().Be(new DateOnly(2015, 6, 1));
    }

    [Test]
    public void AttachmentTarget_Referral_IsItsOwnKindOwnedByItsPatient()
    {
        var referral = CreateReferral();
        var target = new AttachmentTarget(referral.PatientId, null, null) { Referral = referral };

        target.Kind.Should().Be(AttachmentTargetKind.Referral);
        target.Owner.Should().BeSameAs(referral.Patient);
        target.WhyFilesCantChange().Should().BeNull();
        AttachmentTarget.KindOf(new Attachment { ReferralId = referral.Id }).Should().Be(AttachmentTargetKind.Referral);

        referral.Status = ReferralStatus.EnteredInError;
        target.WhyFilesCantChange().Should().Be(ReferralWorkflow.EnteredInErrorFilesMessage);
    }

    [Test]
    public async Task ArchiveSpecialty_WithReferralsAwaitingTriage_IsAConflict()
    {
        var specialty = SpecialtiesTestConstants.CreateSpecialty();
        var repository = SpecialtiesTestConstants.RepositoryWith(specialty);
        repository.Setup(r => r.GetUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, SpecialtyUsage> { [specialty.Id] = new(0, 0, 0, 4, 2) });
        var handler = new SetSpecialtyArchivedHandler(repository.Object, new FixedTimeProvider(NowUtc));

        var act = () => handler.Handle(new SetSpecialtyArchivedCommand(specialty.Id, true, specialty.Version), CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Message.Should().Contain("2 referrals awaiting triage");
        specialty.IsArchived.Should().BeFalse();
    }

    [Test]
    public void SpecialtyUsage_ReferralsCountAsInUse_AndAreNamed()
    {
        var usage = new SpecialtyUsage(0, 0, 0, 1);

        usage.IsInUse.Should().BeTrue();
        SpecialtyRules.InUseMessage(SpecialtiesTestConstants.CreateSpecialty(), usage).Should().Contain("1 referral");
        SpecialtyRules.InUseMessage(SpecialtiesTestConstants.CreateSpecialty(), new SpecialtyUsage(2, 1, 0, 3))
            .Should().Contain("2 pathway templates, 1 pathway and 3 referrals");
    }
}
