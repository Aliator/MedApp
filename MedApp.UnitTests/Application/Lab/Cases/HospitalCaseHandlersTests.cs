using FluentAssertions;
using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Lab.Cases.Commands;
using MedApp.Application.Lab.Cases.Queries;
using MedApp.Application.Lab.Common;
using MedApp.Domain.Hospitals;
using MedApp.Domain.Lab;
using MedApp.Domain.Patients;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Specimens;
using MedApp.UnitTests.Common.Constants;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.TheLab.Cases;

/// <summary>Hospital cases (MC18, MC20): the report sent back on authorising, surgery details, and a case opened by hand.</summary>
[TestFixture]
public sealed class HospitalCaseHandlersTests : LabCaseTestBase
{
    private AuthoriseLabReportHandler Authorise =>
        new(CaseAccess, LabCases.Object, Pathways.Object, Identity.Object, Notifications.Object, Clock);

    private AddSurgeryDetailsHandler SurgeryDetails => new(CaseAccess, LabCases.Object, Clock);

    private OpenLabCaseHandler Open => new(CaseAccess, LabCases.Object, Repository.Object, Pathways.Object, Boundary.Object, Receipt, Clock);

    private HospitalRequest _request = null!;

    private LabCase GivenHospitalCase(LabCaseStatus status = LabCaseStatus.Open, bool readyToReport = false)
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(), FirstName = "Synthetic", LastName = "Hospital-Patient", DateOfBirth = new DateOnly(1960, 1, 1),
            NhsNumber = HospitalsTestConstants.RequestNhsNumber,
        };
        _request = HospitalsTestConstants.Request(HospitalsTestConstants.Hospital(), patient, Histology, Biopsy);
        var labCase = HospitalsTestConstants.Case(_request, status);
        labCase.Steps.Add(Step(labCase, 0, LabCaseStepKind.Received));
        if (readyToReport)
        {
            foreach (var step in ThroughRequiredSteps(labCase))
                labCase.Steps.Add(step);
            labCase.Steps.Add(Step(labCase, 5, LabCaseStepKind.Outcome, choice: LabChoice.NeedsMoreWork, outcome: LabOutcome.NoMoreWork));
        }

        LabCases.Setup(l => l.GetForUpdateAsync(labCase.Id, It.IsAny<CancellationToken>())).ReturnsAsync(labCase);
        LabCases.Setup(l => l.GetByIdAsync(labCase.Id, It.IsAny<CancellationToken>())).ReturnsAsync(labCase);
        return labCase;
    }

    private static LabCase AwaitingDetails(LabCase labCase)
    {
        labCase.Status = LabCaseStatus.AwaitingSurgeryDetails;
        labCase.ReportedAtUtc = Now.AddHours(-3);
        labCase.ReportSentAtUtc = Now.AddHours(-3);
        return labCase;
    }

    [Test]
    public async Task Authorise_AHospitalCase_SendsItsReportToTheHospital_AndAwaitsSurgeryDetails()
    {
        var labCase = GivenHospitalCase(readyToReport: true);

        var response = await Authorise.Handle(new AuthoriseLabReportCommand(labCase.Id, "Synthetic results.", 2, Pathologist), Cts.Token);

        response.Status.Should().Be("AwaitingSurgeryDetails");
        response.HospitalReport.Should().BeEquivalentTo(new
        {
            Status = "Final",
            LetterKind = "LabReport",
            HospitalId = _request.HospitalId,
            HospitalName = HospitalsTestConstants.HospitalName,
            AddressedTo = HospitalsTestConstants.SurgeonName,
            DeliveryMethod = "SecureEmail",
        });
        response.HospitalReport!.SentAtUtc.Should().BeCloseTo(Now, TimeSpan.FromSeconds(1));
        response.HospitalReport.IssuedAtUtc.Should().Be(labCase.ReportedAtUtc!.Value);
        response.CanAddSurgeryDetails.Should().BeTrue("the lab adds the hospital's surgery details next");
        SavedResults.Should().BeEmpty("a hospital case's report goes to the hospital, not to a clinician's sign-off");
        var sent = SavedAudit.Should().ContainSingle(e => e.Action == PatientPathwayAuditAction.LabReportSent).Subject;
        sent.Details.Should().Contain("\"letterKind\":\"LabReport\"").And.Contain("\"recipientKind\":\"Hospital\"")
            .And.Contain("\"method\":\"SecureEmail\"").And.NotContain(HospitalsTestConstants.SurgeonName);
        SavedAudit.Should().AllSatisfy(e => e.Details.Should().NotContain("Synthetic results"));
        labCase.ReportSentByUserId.Should().Be(Pathologist.UserId);
    }

    [Test]
    public async Task SurgeryDetails_OnACaseAwaitingThem_CloseIt_AndAuditNoText()
    {
        var labCase = AwaitingDetails(GivenHospitalCase());

        var response = await SurgeryDetails.Handle(new AddSurgeryDetailsCommand(
            labCase.Id, new DateOnly(2026, 11, 15), " Synthetic excision ", "Synthetic: completed", false, null, 2, LabMember), Cts.Token);

        response.Status.Should().Be("Closed");
        response.ClosedAtUtc.Should().Be(Now);
        response.CanAddSurgeryDetails.Should().BeFalse();
        response.SurgeryDetails.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            SurgeryOn = (DateOnly?)new DateOnly(2026, 11, 15),
            Procedure = "Synthetic excision",
            Outcome = "Synthetic: completed",
            NoSurgeryExpected = false,
        });
        SavedAudit.Select(e => e.Action).Should().Equal(PatientPathwayAuditAction.SurgeryDetailsAdded, PatientPathwayAuditAction.LabCaseClosed);
        SavedAudit.Should().AllSatisfy(e => e.Details.Should().NotContain("Synthetic"));
        TrackedCases.Should().ContainSingle().Which.NewSurgeryDetails.Should().ContainSingle();
    }

    [Test]
    public async Task SurgeryDetails_BeforeTheReport_AreKept_AndTheCaseStaysOpen()
    {
        var labCase = GivenHospitalCase();

        var response = await SurgeryDetails.Handle(new AddSurgeryDetailsCommand(
            labCase.Id, null, null, null, false, "Synthetic: surgery booked", 2, LabMember), Cts.Token);

        response.Status.Should().Be("Open");
        response.CanAddSurgeryDetails.Should().BeTrue();
        response.SurgeryDetails.Should().ContainSingle(d => d.Detail == "Synthetic: surgery booked");
        SavedAudit.Should().ContainSingle(e => e.Action == PatientPathwayAuditAction.SurgeryDetailsAdded);
    }

    [Test]
    public async Task SurgeryDetails_AreRefused_ForAClinicsCase_AClosedOne_AnotherTeam_OrNoSurgeryBeforeTheReport()
    {
        var clinic = GivenCase(LabPathway());
        (await FluentActions.Awaiting(() => SurgeryDetails.Handle(
                new AddSurgeryDetailsCommand(clinic.Id, null, "x", null, false, null, 2, LabMember), Cts.Token))
            .Should().ThrowAsync<ConflictException>()).WithMessage(LabCaseRules.SurgeryDetailsHospitalOnlyMessage);

        var closed = GivenHospitalCase(LabCaseStatus.Closed);
        (await FluentActions.Awaiting(() => SurgeryDetails.Handle(
                new AddSurgeryDetailsCommand(closed.Id, null, "x", null, false, null, 2, LabMember), Cts.Token))
            .Should().ThrowAsync<ConflictException>()).WithMessage(LabCaseRules.SurgeryDetailsClosedMessage);

        var open = GivenHospitalCase();
        (await FluentActions.Awaiting(() => SurgeryDetails.Handle(
                new AddSurgeryDetailsCommand(open.Id, null, "x", null, false, null, 2, ClinicStaff), Cts.Token))
            .Should().ThrowAsync<ConflictException>()).WithMessage(LabCaseRules.LabWorkMessage);
        (await FluentActions.Awaiting(() => SurgeryDetails.Handle(
                new AddSurgeryDetailsCommand(open.Id, null, null, null, true, "Synthetic: none planned", 2, LabMember), Cts.Token))
            .Should().ThrowAsync<ValidationException>()).WithMessage($"*{LabCaseRules.NoSurgeryNotSentMessage}*");
        (await FluentActions.Awaiting(() => SurgeryDetails.Handle(
                new AddSurgeryDetailsCommand(open.Id, null, "x", null, false, null, 1, LabMember), Cts.Token))
            .Should().ThrowAsync<ConflictException>()).WithMessage(LabCaseRules.ChangedMeanwhileMessage);
    }

    [Test]
    public async Task NoSurgeryExpected_OnceSent_ClosesTheCase()
    {
        var labCase = AwaitingDetails(GivenHospitalCase());

        var response = await SurgeryDetails.Handle(new AddSurgeryDetailsCommand(
            labCase.Id, null, null, null, true, "Synthetic: treated elsewhere", 2, LabMember), Cts.Token);

        response.Status.Should().Be("Closed");
        response.SurgeryDetails.Single().NoSurgeryExpected.Should().BeTrue();
        SavedAudit.First().Details.Should().Contain("\"noSurgeryExpected\":true");
    }

    [Test]
    public void SurgeryDetailsValidator_ChecksWhatTheHospitalSent()
    {
        var validator = new AddSurgeryDetailsValidator();
        var id = Guid.NewGuid();

        validator.Validate(new AddSurgeryDetailsCommand(id, null, " ", null, false, null, 1, LabMember)).Errors
            .Should().ContainSingle(e => e.ErrorMessage == LabCaseRules.SurgeryDetailsMissingMessage);
        validator.Validate(new AddSurgeryDetailsCommand(id, new DateOnly(2026, 11, 1), null, null, true, "why", 1, LabMember)).Errors
            .Should().ContainSingle(e => e.ErrorMessage == LabCaseRules.NoSurgeryFieldsMessage);
        validator.Validate(new AddSurgeryDetailsCommand(id, null, null, null, true, " ", 1, LabMember)).Errors
            .Should().ContainSingle(e => e.ErrorMessage == LabCaseRules.NoSurgeryWhyMessage);
        validator.Validate(new AddSurgeryDetailsCommand(id, null, new string('x', 201), null, false, null, 1, LabMember)).Errors
            .Should().ContainSingle(e => e.ErrorMessage == "The procedure must be at most 200 characters.");
        validator.Validate(new AddSurgeryDetailsCommand(id, null, "x", null, false, null, 0, LabMember)).Errors
            .Should().ContainSingle(e => e.ErrorMessage == LabCaseValidation.VersionMessage);
        validator.Validate(new AddSurgeryDetailsCommand(id, null, "x", null, false, null, 1, LabMember)).IsValid.Should().BeTrue();
    }

    [Test]
    public async Task Open_AHospitalSampleByHand_PointsItsRequestAtTheNewCase()
    {
        var first = GivenHospitalCase(LabCaseStatus.EnteredInError);
        var sample = first.Specimens.Single();
        first.Specimens.Clear();
        sample.LabCaseId = null;
        sample.LabCase = null;
        sample.Version = 3;
        Given(sample);

        var response = await Open.Handle(new OpenLabCaseCommand(sample.Id, Histology.Id, 3, LabMember), Cts.Token);

        _request.LabCaseId.Should().Be(response.Id).And.NotBe(first.Id);
        _request.LabCaseNumber.Should().Be(response.Number);
        response.HospitalName.Should().Be(HospitalsTestConstants.HospitalName);
        response.HospitalRequestId.Should().Be(_request.Id);
        response.Clock!.HospitalDueBasis.Should().Be("HospitalDefault", "the request's hospital sets its side of the clock");
    }

    [Test]
    public void Search_AwaitingSurgeryDetails_IsOrderedByWhenTheReportWasSent()
    {
        var older = AwaitingDetails(GivenHospitalCase());
        older.ReportSentAtUtc = Now.AddDays(-5);
        older.ReportedAtUtc = Now.AddDays(-1);
        var newer = AwaitingDetails(GivenHospitalCase());
        newer.ReportSentAtUtc = Now.AddDays(-1);
        newer.ReportedAtUtc = Now.AddDays(-6);
        var query = new SearchLabCasesQuery(1, 10, "AwaitingSurgeryDetails", null, null, null, null, true, LabMember);

        new[] { newer, older }.AsQueryable().Filter(query).Sort(query.ParsedView, null, true).Select(c => c.Id)
            .Should().Equal(older.Id, newer.Id);
        LabCaseSearch.Arrange([newer, older], query, Now).Select(c => c.Id).Should().Equal(older.Id, newer.Id);
    }

    [Test]
    public void Search_FindsAHospitalCaseByItsHospitalsName()
    {
        var labCase = GivenHospitalCase();
        var query = new SearchLabCasesQuery(1, 10, "All", null, null, "riverside", null, true, LabMember);

        new[] { labCase }.AsQueryable().Filter(query).Should().ContainSingle();
    }
}
