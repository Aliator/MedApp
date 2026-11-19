using FluentAssertions;
using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Specimens.Commands.EditSpecimen;
using MedApp.Application.Specimens.Commands.MarkSpecimenEnteredInError;
using MedApp.Application.Specimens.Commands.MarkSpecimenLost;
using MedApp.Application.Specimens.Commands.MarkSpecimenReceived;
using MedApp.Application.Specimens.Commands.MarkSpecimenSent;
using MedApp.Application.Specimens.Commands.RecordSpecimen;
using MedApp.Application.Specimens.Commands.RejectSpecimen;
using MedApp.Application.Specimens.Common;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Specimens;
using MedApp.UnitTests.Common.Constants;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using static MedApp.UnitTests.Common.Constants.SpecimensTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Specimens.Commands;

[TestFixture]
public sealed class RecordSpecimenHandlerTests : SpecimenTestBase
{
    private RecordSpecimenHandler Handler =>
        new(Pathways.Object, Repository.Object, Holders.Object, Identity.Object, QrCodes.Object, Options, Teams.Object, Access.Object, Clock);

    private RecordSpecimenCommand Command(PatientPathway pathway, DateTime? takenAt = null, Guid? requesting = null, int stage = 1) => new(
        pathway.Id, pathway.StageAt(stage).Id, Biopsy.Id, InHouse.Id, takenAt, "  left forearm ", " ", requesting ?? Nurse.UserId, Nurse);

    [Test]
    public async Task Handle_CurrentStage_AddsItTakenWithItsNumberAndAuditsIt()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));

        var response = await Handler.Handle(Command(pathway), Cts.Token);

        var specimen = Added.Should().ContainSingle().Subject;
        specimen.Number.Should().Be("MA26-000123");
        specimen.Status.Should().Be(SpecimenStatus.Taken);
        specimen.Version.Should().Be(1);
        specimen.TakenAtUtc.Should().Be(Now);
        specimen.RecordedAtUtc.Should().Be(Now);
        specimen.TakenByUserId.Should().Be(Nurse.UserId);
        specimen.Site.Should().Be("left forearm");
        specimen.RequestNotes.Should().BeNull();
        specimen.PatientId.Should().Be(pathway.PatientId);
        var entry = SavedAudit.Should().ContainSingle().Subject;
        entry.Action.Should().Be(PatientPathwayAuditAction.SpecimenTaken);
        entry.Details.Should().NotContain("left forearm");
        Tokens.Should().Equal(Cts.Token);
        response.Number.Should().Be("MA26-000123");
        response.Label.Size.Should().Be(29);
        QrCodes.Verify(q => q.Render("MA26-000123"), Times.Once, "the QR code holds the number only");
    }

    [Test]
    public async Task Handle_TakenTimeTypedBack_KeepsIt()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));

        await Handler.Handle(Command(pathway, Now.AddDays(-3)), Cts.Token);

        Added.Single().TakenAtUtc.Should().Be(Now.AddDays(-3));
    }

    [TestCase(1)]
    [TestCase(-8 * 24)]
    public async Task Handle_TakenTimeInTheFutureOrOverAWeekBack_Throws400(int hours)
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));

        var act = () => Handler.Handle(Command(pathway, Now.AddHours(hours)), Cts.Token);

        await act.Should().ThrowAsync<ValidationException>();
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_NotTheCurrentStage_Throws409()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));

        var act = () => Handler.Handle(Command(pathway, stage: 2), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*being worked on*");
    }

    [TestCase(Status.Blocked)]
    [TestCase(Status.Completed)]
    [TestCase(Status.Cancelled)]
    public async Task Handle_PathwayNotOpen_Throws409(Status status)
    {
        var pathway = GivenPathway(CreatePathway(status));

        var act = () => Handler.Handle(Command(pathway), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Handle_RestrictedRecord_Throws409SayingToAskAnAdmin()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.RestrictedAtUtc = Now.AddDays(-1);
        var pathway = GivenPathway(CreatePathway(Status.InProgress, patient: patient));

        var act = () => Handler.Handle(Command(pathway), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(SpecimenWorkflow.RestrictedTakeMessage);
    }

    [Test]
    public async Task Handle_RequestingClinicianCantWorkOnPathways_Throws400()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));

        var act = () => Handler.Handle(Command(pathway, requesting: OtherUserId), Cts.Token);

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Should().ContainSingle(e => e.PropertyName == "RequestingUserId");
    }

    [Test]
    public async Task Handle_ArchivedType_Throws400()
    {
        Biopsy.IsArchived = true;
        var pathway = GivenPathway(CreatePathway(Status.InProgress));

        var act = () => Handler.Handle(Command(pathway), Cts.Token);

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Should().ContainSingle(e => e.PropertyName == "SpecimenTypeId");
    }

    [Test]
    public async Task Handle_UnknownPathway_Throws404()
    {
        var act = () => Handler.Handle(Command(CreatePathway(Status.InProgress)), Cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public void Validator_TooLongSiteAndNotes_AreRefused()
    {
        var pathway = CreatePathway();
        var command = Command(pathway) with { Site = new string('x', 201), RequestNotes = new string('x', 1001) };

        new RecordSpecimenValidator().Validate(command).Errors.Select(e => e.PropertyName)
            .Should().BeEquivalentTo(["Site", "RequestNotes"]);
    }
}

[TestFixture]
public sealed class EditSpecimenHandlerTests : SpecimenTestBase
{
    private EditSpecimenHandler Handler => new(Repository.Object, Holders.Object, Identity.Object, QrCodes.Object, Clock);

    [Test]
    public async Task Handle_Taken_ChangesItAndAuditsTheFieldsByName()
    {
        var specimen = Given(CreateSpecimen());
        var urine = Urine();
        Repository.Setup(r => r.GetTypeAsync(urine.Id, It.IsAny<CancellationToken>())).ReturnsAsync(urine);

        await Handler.Handle(new EditSpecimenCommand(
            specimen.Id, urine.Id, InHouse.Id, specimen.TakenAtUtc, "right forearm", specimen.RequestNotes, Admin.UserId, 3, Nurse), Cts.Token);

        specimen.SpecimenTypeId.Should().Be(urine.Id);
        specimen.Site.Should().Be("right forearm");
        specimen.RequestingUserId.Should().Be(Admin.UserId);
        specimen.Version.Should().Be(4);
        var entry = SavedAudit.Should().ContainSingle().Subject;
        entry.Action.Should().Be(PatientPathwayAuditAction.SpecimenDetailsEdited);
        entry.Details.Should().Contain("\"site\"").And.NotContain("right forearm");
        entry.Details.Should().Contain(UrineName).And.Contain(Admin.Username);
    }

    [Test]
    public async Task Handle_NothingChanged_SavesNothing()
    {
        var specimen = Given(CreateSpecimen());

        await Handler.Handle(new EditSpecimenCommand(
            specimen.Id, Biopsy.Id, InHouse.Id, specimen.TakenAtUtc, specimen.Site, specimen.RequestNotes, specimen.RequestingUserId, 3, Nurse), Cts.Token);

        SavedAudit.Should().BeEmpty();
        specimen.Version.Should().Be(3);
    }

    [Test]
    public async Task Handle_AlreadySent_Throws409()
    {
        var specimen = Given(CreateSpecimen(status: SpecimenStatus.Sent));

        var act = () => Handler.Handle(new EditSpecimenCommand(
            specimen.Id, Biopsy.Id, InHouse.Id, specimen.TakenAtUtc, "x", null, specimen.RequestingUserId, 3, Nurse), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*before it's sent*");
    }

    [Test]
    public async Task Handle_StaleVersion_Throws409()
    {
        var specimen = Given(CreateSpecimen());

        var act = () => Handler.Handle(new EditSpecimenCommand(
            specimen.Id, Biopsy.Id, InHouse.Id, specimen.TakenAtUtc, "x", null, specimen.RequestingUserId, 2, Nurse), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(SpecimenWorkflow.ChangedMeanwhileMessage);
    }

    [Test]
    public async Task Handle_ArchivedTypeItAlreadyHas_IsAllowed()
    {
        var specimen = Given(CreateSpecimen());
        Biopsy.IsArchived = true;

        var act = () => Handler.Handle(new EditSpecimenCommand(
            specimen.Id, Biopsy.Id, InHouse.Id, specimen.TakenAtUtc, "elsewhere", null, specimen.RequestingUserId, 3, Nurse), Cts.Token);

        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task Handle_RestrictedRecord_Throws409()
    {
        var specimen = Given(CreateSpecimen());
        specimen.Patient.RestrictedAtUtc = Now;

        var act = () => Handler.Handle(new EditSpecimenCommand(
            specimen.Id, Biopsy.Id, InHouse.Id, specimen.TakenAtUtc, "x", null, specimen.RequestingUserId, 3, Nurse), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.RestrictedMessage);
    }
}

[TestFixture]
public sealed class SpecimenStepHandlersTests : SpecimenTestBase
{
    [Test]
    public async Task Sent_Taken_RecordsItWithTheReferenceAndAudits()
    {
        var specimen = Given(CreateSpecimen());

        var response = await new MarkSpecimenSentHandler(Repository.Object, Identity.Object, QrCodes.Object, Clock)
            .Handle(new MarkSpecimenSentCommand(specimen.Id, null, " BATCH-7 ", 3, Nurse), Cts.Token);

        specimen.Status.Should().Be(SpecimenStatus.Sent);
        specimen.SentAtUtc.Should().Be(Now);
        specimen.SentByUserId.Should().Be(Nurse.UserId);
        specimen.CourierReference.Should().Be("BATCH-7");
        SavedAudit.Single().Action.Should().Be(PatientPathwayAuditAction.SpecimenSent);
        SavedAudit.Single().Details.Should().Contain("\"from\":\"Taken\"").And.Contain("\"to\":\"Sent\"").And.NotContain("BATCH-7");
        Tokens.Should().Equal(Cts.Token);
        response.Status.Should().Be("Sent");
    }

    [Test]
    public async Task Sent_BeforeItWasTaken_Throws400()
    {
        var specimen = Given(CreateSpecimen(takenAtUtc: Now.AddHours(-2)));

        var act = () => new MarkSpecimenSentHandler(Repository.Object, Identity.Object, QrCodes.Object, Clock)
            .Handle(new MarkSpecimenSentCommand(specimen.Id, Now.AddHours(-3), null, 3, Nurse), Cts.Token);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task Sent_RestrictedRecord_StillGoesToTheLab()
    {
        var specimen = Given(CreateSpecimen());
        specimen.Patient.RestrictedAtUtc = Now;

        await new MarkSpecimenSentHandler(Repository.Object, Identity.Object, QrCodes.Object, Clock)
            .Handle(new MarkSpecimenSentCommand(specimen.Id, null, null, 3, Nurse), Cts.Token);

        specimen.Status.Should().Be(SpecimenStatus.Sent);
    }

    [Test]
    public async Task Received_NotSent_Throws409()
    {
        var specimen = Given(CreateSpecimen());

        var act = () => new MarkSpecimenReceivedHandler(Repository.Object, Identity.Object, QrCodes.Object, Clock)
            .Handle(new MarkSpecimenReceivedCommand(specimen.Id, null, null, 3, LabStaff), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*as sent*");
    }

    [Test]
    public async Task Received_Sent_RecordsItWithTheLabsReference()
    {
        var specimen = Given(CreateSpecimen(status: SpecimenStatus.Sent));

        await new MarkSpecimenReceivedHandler(Repository.Object, Identity.Object, QrCodes.Object, Clock)
            .Handle(new MarkSpecimenReceivedCommand(specimen.Id, null, "LAB-99", 3, LabStaff), Cts.Token);

        specimen.Status.Should().Be(SpecimenStatus.Received);
        specimen.ReceivedByUserId.Should().Be(LabStaff.UserId);
        specimen.LabReference.Should().Be("LAB-99");
        SavedAudit.Single().Action.Should().Be(PatientPathwayAuditAction.SpecimenReceived);
    }

    [Test]
    public async Task Received_AnonymisedRecord_Throws409()
    {
        var specimen = Given(CreateSpecimen(status: SpecimenStatus.Sent));
        specimen.Patient.AnonymisedAtUtc = Now;

        var act = () => new MarkSpecimenReceivedHandler(Repository.Object, Identity.Object, QrCodes.Object, Clock)
            .Handle(new MarkSpecimenReceivedCommand(specimen.Id, null, null, 3, LabStaff), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.AnonymisedMessage);
    }

    [Test]
    public async Task Rejected_TellsTheRequesterAndTakerButNotTheLab_WithoutTheDetail()
    {
        var specimen = Given(CreateSpecimen(status: SpecimenStatus.Received));
        specimen.RequestingUserId = Admin.UserId;

        await new RejectSpecimenHandler(Repository.Object, Notifications.Object, Identity.Object, QrCodes.Object, Clock)
            .Handle(new RejectSpecimenCommand(specimen.Id, "insufficientSample", "Synthetic: too little", 3, LabStaff), Cts.Token);

        specimen.Status.Should().Be(SpecimenStatus.RejectedByLab);
        specimen.RejectionReason.Should().Be(SpecimenRejectionReason.InsufficientSample);
        specimen.ClosedDetail.Should().Be("Synthetic: too little");
        specimen.ClosedByUserId.Should().Be(LabStaff.UserId);
        SavedAudit.Single().Details.Should().Contain("InsufficientSample").And.NotContain("too little");
        Sent.Select(n => n.UserId).Should().BeEquivalentTo([Admin.UserId, Nurse.UserId]);
        Sent.Should().AllSatisfy(n =>
        {
            n.Kind.Should().Be(NotificationKind.SpecimenRejectedByLab);
            n.Title.Should().Be("Specimen rejected");
            n.Message.Should().Be($"The lab rejected a {BiopsyName} specimen.");
            n.SubjectId.Should().Be(specimen.Id);
            n.PathwayId.Should().Be(specimen.PathwayId);
        });
    }

    [Test]
    public async Task Rejected_Taken_Throws409()
    {
        var specimen = Given(CreateSpecimen());

        var act = () => new RejectSpecimenHandler(Repository.Object, Notifications.Object, Identity.Object, QrCodes.Object, Clock)
            .Handle(new RejectSpecimenCommand(specimen.Id, "Leaked", null, 3, LabStaff), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>();
        Sent.Should().BeEmpty();
    }

    [Test]
    public void RejectValidator_OtherWithoutDetail_IsRefused()
    {
        new RejectSpecimenValidator().Validate(new RejectSpecimenCommand(Guid.NewGuid(), "Other", " ", 1, LabStaff))
            .Errors.Should().ContainSingle(e => e.PropertyName == "Detail");
        new RejectSpecimenValidator().Validate(new RejectSpecimenCommand(Guid.NewGuid(), "Spilled", null, 1, LabStaff))
            .Errors.Should().ContainSingle(e => e.PropertyName == "Reason");
    }

    [Test]
    public async Task Lost_ByTheTaker_TellsOnlyTheRequesterWhenTheyDiffer()
    {
        var specimen = Given(CreateSpecimen(status: SpecimenStatus.Sent));

        await new MarkSpecimenLostHandler(Repository.Object, Notifications.Object, Identity.Object, QrCodes.Object, Clock)
            .Handle(new MarkSpecimenLostCommand(specimen.Id, "Synthetic: not in the bag", 3, Nurse), Cts.Token);

        specimen.Status.Should().Be(SpecimenStatus.Lost);
        Sent.Should().BeEmpty("the nurse asked for it, took it and marked it lost");
    }

    [Test]
    public async Task Lost_ByTheLab_TellsTheRequester()
    {
        var specimen = Given(CreateSpecimen(status: SpecimenStatus.Sent));

        await new MarkSpecimenLostHandler(Repository.Object, Notifications.Object, Identity.Object, QrCodes.Object, Clock)
            .Handle(new MarkSpecimenLostCommand(specimen.Id, "Synthetic: not in the bag", 3, LabStaff), Cts.Token);

        var notification = Sent.Should().ContainSingle().Subject;
        notification.Kind.Should().Be(NotificationKind.SpecimenLost);
        notification.Message.Should().Be($"{LabStaff.Username} marked a {BiopsyName} specimen lost.");
    }

    [Test]
    public async Task Lost_WithoutASpecimenPermission_Throws403()
    {
        var specimen = Given(CreateSpecimen(status: SpecimenStatus.Sent));

        var act = () => new MarkSpecimenLostHandler(Repository.Object, Notifications.Object, Identity.Object, QrCodes.Object, Clock)
            .Handle(new MarkSpecimenLostCommand(specimen.Id, "x", 3, Staff), Cts.Token);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Test]
    public async Task EnteredInError_ByTheRecorderWithin24Hours_ClosesIt()
    {
        var specimen = Given(CreateSpecimen(recordedAtUtc: Now.AddHours(-23)));

        await new MarkSpecimenEnteredInErrorHandler(Repository.Object, Identity.Object, QrCodes.Object, Clock)
            .Handle(new MarkSpecimenEnteredInErrorCommand(specimen.Id, "Synthetic: wrong patient", 3, Nurse), Cts.Token);

        specimen.Status.Should().Be(SpecimenStatus.EnteredInError);
        SavedAudit.Single().Action.Should().Be(PatientPathwayAuditAction.SpecimenEnteredInError);
    }

    [Test]
    public async Task EnteredInError_ByTheRecorderAfter24Hours_Throws403()
    {
        var specimen = Given(CreateSpecimen(recordedAtUtc: Now.AddHours(-25)));

        var act = () => new MarkSpecimenEnteredInErrorHandler(Repository.Object, Identity.Object, QrCodes.Object, Clock)
            .Handle(new MarkSpecimenEnteredInErrorCommand(specimen.Id, "x", 3, Nurse), Cts.Token);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage(SpecimenWorkflow.EnteredInErrorNotAllowedMessage);
    }

    [Test]
    public async Task EnteredInError_ByCorrectRecordsAnyTime_ClosesIt_AlsoWhileRestricted()
    {
        var specimen = Given(CreateSpecimen(status: SpecimenStatus.Received, recordedAtUtc: Now.AddDays(-10)));
        specimen.Patient.RestrictedAtUtc = Now;

        await new MarkSpecimenEnteredInErrorHandler(Repository.Object, Identity.Object, QrCodes.Object, Clock)
            .Handle(new MarkSpecimenEnteredInErrorCommand(specimen.Id, "x", 3, Corrector), Cts.Token);

        specimen.Status.Should().Be(SpecimenStatus.EnteredInError);
    }

    [Test]
    public async Task EnteredInError_Reported_Throws409()
    {
        var specimen = Given(CreateSpecimen(status: SpecimenStatus.Reported));

        var act = () => new MarkSpecimenEnteredInErrorHandler(Repository.Object, Identity.Object, QrCodes.Object, Clock)
            .Handle(new MarkSpecimenEnteredInErrorCommand(specimen.Id, "x", 3, Corrector), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(SpecimenWorkflow.ClosedMessage(SpecimenStatus.Reported));
    }

    [Test]
    public void CloseValidators_NeedADetailAndAVersion()
    {
        new MarkSpecimenLostValidator().Validate(new MarkSpecimenLostCommand(Guid.NewGuid(), " ", 0, Nurse))
            .Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(["Detail", "ExpectedVersion"]);
        new MarkSpecimenEnteredInErrorValidator().Validate(new MarkSpecimenEnteredInErrorCommand(Guid.NewGuid(), new string('x', 501), 1, Nurse))
            .Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(["Detail"]);
        new MarkSpecimenSentValidator().Validate(new MarkSpecimenSentCommand(Guid.NewGuid(), null, new string('x', 51), 1, Nurse))
            .Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(["CourierReference"]);
        new MarkSpecimenReceivedValidator().Validate(new MarkSpecimenReceivedCommand(Guid.NewGuid(), null, null, 0, LabStaff))
            .Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(["ExpectedVersion"]);
        new EditSpecimenValidator().Validate(new EditSpecimenCommand(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Now, null, null, Guid.NewGuid(), 1, Nurse))
            .Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(["SpecimenTypeId"]);
    }
}
