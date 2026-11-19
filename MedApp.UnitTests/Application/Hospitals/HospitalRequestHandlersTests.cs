using FluentAssertions;
using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Paging;
using MedApp.Application.Common.Teams;
using MedApp.Application.Hospitals.Common;
using MedApp.Application.Hospitals.Repositories;
using MedApp.Application.Hospitals.Requests;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Hospitals.Requests;
using MedApp.Domain.Activity;
using MedApp.Domain.Hospitals;
using MedApp.Domain.Lab;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Patients;
using MedApp.Domain.Specimens;
using MedApp.UnitTests.Application.TheLab;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Hospitals;

/// <summary>Hospital requests (MC18): registering one, the patient matches, the intake list and one request.</summary>
[TestFixture]
public sealed class HospitalRequestHandlersTests : LabCaseTestBase
{
    private Mock<IHospitalRepository> _hospitals = null!;
    private RecordingActivityLog _activity = null!;
    private Hospital _hospital = null!;
    private Patient _patient = null!;
    private HospitalRequest? _tracked;
    private Specimen? _trackedSample;
    private Patient? _trackedPatient;

    private static readonly PathwayActor ViewingAdmin = new(
        Admin.UserId, Admin.Username, new HashSet<MedApp.Application.Common.Identity.Permission>
        {
            MedApp.Application.Common.Identity.Permission.ViewPatients, MedApp.Application.Common.Identity.Permission.ProcessSpecimens
        },
        new TeamScope(Admin.UserId, [], seesAllTeams: true));

    [SetUp]
    public void SetUpHospitals()
    {
        _hospitals = new Mock<IHospitalRepository>();
        _activity = new RecordingActivityLog();
        _tracked = null;
        _trackedSample = null;
        _trackedPatient = null;
        _hospital = HospitalsTestConstants.Hospital();
        _patient = new Patient
        {
            Id = Guid.NewGuid(), FirstName = "Synthetic", LastName = "Hospital-Patient", DateOfBirth = new DateOnly(1960, 1, 1),
            NhsNumber = "9990000026",
        };
        _hospitals.Setup(r => r.GetAsync(_hospital.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_hospital);
        _hospitals.Setup(r => r.GetPatientForUpdateAsync(_patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_patient);
        _hospitals
            .Setup(r => r.TrackRegistration(
                It.IsAny<HospitalRequest>(), It.IsAny<Specimen>(), It.IsAny<Patient?>(), It.IsAny<ActivityEntry?>(),
                It.IsAny<IEnumerable<PatientPathwayAuditEntry>>()))
            .Callback<HospitalRequest, Specimen, Patient?, ActivityEntry?, IEnumerable<PatientPathwayAuditEntry>>((r, s, p, _, audit) =>
            {
                _tracked = r;
                _trackedSample = s;
                _trackedPatient = p;
                SavedAudit.AddRange(audit);
            });
    }

    private RegisterHospitalRequestHandler Register =>
        new(_hospitals.Object, Repository.Object, LabCases.Object, Teams.Object, Receipt, _activity, Clock);

    private RegisterHospitalRequestCommand Command(
        Guid? patientId = null, HospitalPatientRequest? newPatient = null, PathwayActor? actor = null) => new(
        _hospital.Id, patientId, newPatient, " RGH-TEST-002 ", "999 000 0018", " Mr Synthetic Surgeon ", "Urgent", true,
        Now.AddDays(-1), new DateOnly(2026, 12, 1), Now.AddDays(-1), Biopsy.Id, Histology.Id, actor ?? LabMember);

    [Test]
    public async Task Register_ForAChosenPatient_BringsItsSample_AndOpensItsCase_InOneSave()
    {
        var response = await Register.Handle(Command(patientId: _patient.Id), Cts.Token);

        _tracked.Should().NotBeNull();
        _trackedPatient.Should().BeNull("the patient was chosen, not made");
        response.Should().BeEquivalentTo(new
        {
            HospitalId = _hospital.Id,
            HospitalName = HospitalsTestConstants.HospitalName,
            PatientId = _patient.Id,
            HospitalPatientNumber = "RGH-TEST-002",
            NhsNumber = "9990000018",
            NhsNumbersDiffer = true,
            SurgeonName = "Mr Synthetic Surgeon",
            Priority = "Urgent",
            IsCancerPathway = true,
            Source = "Hospital",
            LabCaseNumber = "MA26-000123",
            LabCaseStatus = "Open",
            LabTestName = "Histology",
            RegisteredByUsername = LabMember.Username,
        });
        var sample = _trackedSample!;
        sample.Should().BeEquivalentTo(new
        {
            Number = "MA26-000123", Source = SpecimenSource.Hospital, Status = SpecimenStatus.Received, PathwayId = (Guid?)null,
            HospitalId = (Guid?)_hospital.Id, HospitalRequestId = (Guid?)_tracked!.Id, TeamId = MedApp.Domain.Teams.Team.LabId,
        });
        var opened = TrackedCases.Should().ContainSingle().Subject.Case;
        opened.Should().BeEquivalentTo(new
        {
            Source = SpecimenSource.Hospital, HospitalId = (Guid?)_hospital.Id, HospitalRequestId = (Guid?)_tracked.Id,
            PathwayId = (Guid?)null, Number = "MA26-000123",
        }, o => o.ExcludingMissingMembers());
        opened.Steps.Should().ContainSingle(s => s.Kind == LabCaseStepKind.Received);
        _tracked.LabCaseId.Should().Be(opened.Id);
        sample.LabCaseId.Should().Be(opened.Id);
        SavedAudit.Select(e => e.Action).Should().Contain([PatientPathwayAuditAction.SpecimenReceived, PatientPathwayAuditAction.LabCaseOpened]);
        SavedAudit.Should().AllSatisfy(e => e.Details.Should().NotContain("RGH-TEST").And.NotContain("Synthetic Surgeon"));
        LabCases.Verify(l => l.SaveAsync(Cts.Token), Times.Once);
    }

    [Test]
    public async Task Register_ANewPatient_IsMadeWithIt_AndRecorded()
    {
        var response = await Register.Handle(
            Command(newPatient: new HospitalPatientRequest { FirstName = " New ", LastName = " Synthetic ", DateOfBirth = new DateOnly(1970, 2, 3), NhsNumber = "999 000 0018" }),
            Cts.Token);

        _trackedPatient.Should().NotBeNull();
        _trackedPatient!.Should().BeEquivalentTo(new { FirstName = "New", LastName = "Synthetic", NhsNumber = "9990000018" }, o => o.ExcludingMissingMembers());
        response.PatientId.Should().Be(_trackedPatient.Id);
        response.NhsNumbersDiffer.Should().BeFalse();
    }

    [Test]
    public async Task Register_IsRefused_OutsideTheLab_ForAnAdmin_AnArchivedHospital_ARestrictedPatient_OrANumberInUse()
    {
        (await FluentActions.Awaiting(() => Register.Handle(Command(_patient.Id, actor: ClinicStaff), Cts.Token))
            .Should().ThrowAsync<ConflictException>()).WithMessage(HospitalRequestRules.LabOnlyMessage);
        (await FluentActions.Awaiting(() => Register.Handle(Command(_patient.Id, actor: ViewingAdmin), Cts.Token))
            .Should().ThrowAsync<ConflictException>()).WithMessage(HospitalRequestRules.LabOnlyMessage);

        _hospital.IsArchived = true;
        (await FluentActions.Awaiting(() => Register.Handle(Command(_patient.Id), Cts.Token))
            .Should().ThrowAsync<ValidationException>()).WithMessage($"*'{HospitalsTestConstants.HospitalName}' is archived.*");
        _hospital.IsArchived = false;

        _patient.RestrictedAtUtc = Now.AddDays(-1);
        await FluentActions.Awaiting(() => Register.Handle(Command(_patient.Id), Cts.Token)).Should().ThrowAsync<ConflictException>();

        _hospitals.Setup(r => r.NhsNumberInUseAsync("9990000018", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        (await FluentActions.Awaiting(() => Register.Handle(
                Command(newPatient: new HospitalPatientRequest { FirstName = "A", LastName = "B", DateOfBirth = new DateOnly(1970, 1, 1), NhsNumber = "9990000018" }),
                Cts.Token))
            .Should().ThrowAsync<ConflictException>()).WithMessage(NhsNumberRules.InUse);

        await FluentActions.Awaiting(() => Register.Handle(Command(Guid.NewGuid()), Cts.Token)).Should().ThrowAsync<NotFoundException>();
        _tracked.Should().BeNull();
    }

    [Test]
    public void RegisterValidator_ChecksTheRequest_AndThePatient()
    {
        var validator = new RegisterHospitalRequestValidator(Clock);
        var good = Command(_patient.Id);
        validator.Validate(good).IsValid.Should().BeTrue();

        string[] Messages(RegisterHospitalRequestCommand command) => validator.Validate(command).Errors.Select(e => e.ErrorMessage).ToArray();

        Messages(good with { HospitalPatientNumber = " " }).Should().Equal(HospitalRequestRules.NumberMissingMessage);
        Messages(good with { NhsNumber = "12345" }).Should().Equal(HospitalRequestRules.NhsNumberMessage);
        Messages(good with { NhsNumber = "9990000017" }).Should().Equal(NhsNumberRules.WrongCheckDigit);
        Messages(good with { SurgeonName = "" }).Should().Equal(HospitalRequestRules.SurgeonMissingMessage);
        Messages(good with { Priority = "Soon" }).Should().Equal(HospitalRequestRules.PriorityMessage);
        Messages(good with { AuthoredOn = Now.AddHours(1) }).Should().Equal(HospitalRequestRules.AuthoredOnMessage);
        Messages(good with { CollectedAtUtc = Now.AddHours(1) }).Should().Equal(HospitalRequestRules.CollectedAtMessage);
        Messages(good with { ReportNeededBy = DateOnly.FromDateTime(Now.AddDays(-5)) }).Should().Equal(HospitalRequestRules.NeededByMessage);
        Messages(good with { PatientId = null }).Should().Equal(HospitalRequestRules.NoPatientMessage);
        Messages(good with { NewPatient = new HospitalPatientRequest { FirstName = "A", LastName = "B", DateOfBirth = new DateOnly(1970, 1, 1) } })
            .Should().Equal(HospitalRequestRules.BothPatientsMessage);
        Messages(good with { PatientId = null, NewPatient = new HospitalPatientRequest { FirstName = " ", LastName = "B", DateOfBirth = default } })
            .Should().BeEquivalentTo(HospitalRequestRules.FirstNameMessage, HospitalRequestRules.DateOfBirthMessage);
    }

    [Test]
    public async Task PatientMatches_AreForTheLab_AndEachOneIsRecordedAsListed()
    {
        _hospitals.Setup(r => r.FindPatientMatchesAsync("9990000026", null, null, Cts.Token)).ReturnsAsync([_patient]);
        var handler = new FindHospitalPatientMatchesHandler(_hospitals.Object, Teams.Object, _activity);

        var matches = await handler.Handle(new FindHospitalPatientMatchesQuery("999 000 0026", " ", new DateOnly(1960, 1, 1), LabMember), Cts.Token);

        matches.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            PatientId = _patient.Id, FirstName = "Synthetic", LastName = "Hospital-Patient", NhsNumber = "9990000026", IsRestricted = false,
        });
        _activity.Views.Should().ContainSingle(v => v.Action == ActivityAction.PatientListed && v.PatientIds.Contains(_patient.Id));

        await FluentActions.Awaiting(() => handler.Handle(new FindHospitalPatientMatchesQuery("9990000026", null, null, ClinicStaff), Cts.Token))
            .Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public void PatientMatchesValidator_NeedsTheNhsNumber_OrTheNameAndDateOfBirth()
    {
        var validator = new FindHospitalPatientMatchesValidator();
        validator.Validate(new FindHospitalPatientMatchesQuery(" ", "Synthetic", null, LabMember)).Errors
            .Should().ContainSingle(e => e.ErrorMessage == HospitalRequestRules.MatchMissingMessage);
        validator.Validate(new FindHospitalPatientMatchesQuery(null, "Synthetic", new DateOnly(1960, 1, 1), LabMember)).IsValid.Should().BeTrue();
        validator.Validate(new FindHospitalPatientMatchesQuery("9990000026", null, null, LabMember)).IsValid.Should().BeTrue();
    }

    [Test]
    public async Task Search_ForTheLab_GivesThePage_WithItsCasesStatuses_AndRecordsEachPatient()
    {
        var request = HospitalsTestConstants.Request(_hospital, _patient, Histology, Biopsy);
        _hospitals.Setup(r => r.SearchAsync(It.IsAny<SearchHospitalRequestsQuery>(), Cts.Token))
            .ReturnsAsync(new PagedResult<HospitalRequest>([request], 1));
        _hospitals.Setup(r => r.GetCaseStatusesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), Cts.Token))
            .ReturnsAsync(new Dictionary<Guid, LabCaseStatus> { [request.LabCaseId] = LabCaseStatus.AwaitingSurgeryDetails });
        var handler = new SearchHospitalRequestsHandler(_hospitals.Object, Teams.Object, _activity);

        var page = await handler.Handle(new SearchHospitalRequestsQuery(1, 10, null, null, null, null, true, LabMember), Cts.Token);

        page.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            LabCaseStatus = "AwaitingSurgeryDetails", HospitalName = HospitalsTestConstants.HospitalName,
            PatientName = "Synthetic Hospital-Patient", Priority = "Urgent", Source = "Hospital",
        });
        _activity.Views.Should().ContainSingle(v => v.Action == ActivityAction.PatientListed);
    }

    [Test]
    public async Task Search_OutsideTheLab_IsAnEmptyPage_AndRecordsNothing()
    {
        var handler = new SearchHospitalRequestsHandler(_hospitals.Object, Teams.Object, _activity);

        var page = await handler.Handle(new SearchHospitalRequestsQuery(1, 10, null, null, null, null, true, ClinicStaff), Cts.Token);

        page.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(0);
        _activity.Views.Should().BeEmpty();
        _hospitals.Verify(r => r.SearchAsync(It.IsAny<SearchHospitalRequestsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void SearchFiltersAndOrder_RunInMemory()
    {
        var other = HospitalsTestConstants.Hospital();
        other.Name = "Hillcrest (test)";
        var first = HospitalsTestConstants.Request(
            _hospital, _patient, Histology, Biopsy, priority: LabRequestPriority.Routine, registeredAtUtc: Now.AddDays(-5));
        first.LabCaseNumber = "MA26-000001";
        var second = HospitalsTestConstants.Request(other, _patient, Histology, Biopsy, registeredAtUtc: Now.AddDays(-1));
        second.LabCaseNumber = "MA26-000002";
        var requests = new[] { first, second }.AsQueryable();
        SearchHospitalRequestsQuery Query(string? search = null, string? priority = null, string? sort = null, bool ascending = true, Guid? hospitalId = null) =>
            new(1, 10, hospitalId, priority, search, sort, ascending, LabMember);

        requests.Filter(Query()).Sort(null, true).Select(r => r.Id).Should().Equal(second.Id, first.Id);
        requests.Filter(Query(search: "000001")).Should().ContainSingle().Which.Should().Be(first);
        requests.Filter(Query(search: "999 000 0026")).Should().HaveCount(2);
        requests.Filter(Query(priority: "routine")).Should().ContainSingle().Which.Should().Be(first);
        requests.Filter(Query(hospitalId: other.Id)).Should().ContainSingle().Which.Should().Be(second);
        requests.Filter(Query()).Sort("hospital", true).Select(r => r.Id).Should().Equal(second.Id, first.Id);
    }

    [Test]
    public void SearchValidator_ChecksThePriority_AndTheSearch()
    {
        var validator = new SearchHospitalRequestsValidator();
        validator.Validate(new SearchHospitalRequestsQuery(1, 10, null, "Soon", null, null, true, LabMember)).IsValid.Should().BeFalse();
        validator.Validate(new SearchHospitalRequestsQuery(1, 10, null, null, new string('x', 201), null, true, LabMember)).Errors
            .Should().ContainSingle(e => e.ErrorMessage == "Search must not exceed 200 characters.");
        validator.Validate(new SearchHospitalRequestsQuery(1, 10, null, "Stat", "x", "needed", false, LabMember)).IsValid.Should().BeTrue();
    }

    [Test]
    public async Task Get_ForTheLab_RecordsLabCaseViewed_AndForAClinicIsARecorded404()
    {
        var request = HospitalsTestConstants.Request(_hospital, _patient, Histology, Biopsy);
        _hospitals.Setup(r => r.GetRequestAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _hospitals.Setup(r => r.GetCaseStatusesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, LabCaseStatus>());
        Boundary.Setup(b => b.RefuseAsync(HospitalRequestRules.TargetKind, request.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotFoundException("hidden"));
        var handler = new GetHospitalRequestHandler(_hospitals.Object, Teams.Object, Boundary.Object, Identity.Object, _activity);

        var response = await handler.Handle(new GetHospitalRequestQuery(request.Id, LabMember), Cts.Token);

        response.LabCaseStatus.Should().Be("Open");
        var view = _activity.SingleViews.Should().ContainSingle().Subject;
        view.Action.Should().Be(ActivityAction.LabCaseViewed);
        view.Details.Should().Contain(request.LabCaseId.ToString()).And.Contain("Owner");

        await FluentActions.Awaiting(() => handler.Handle(new GetHospitalRequestQuery(request.Id, ClinicStaff), Cts.Token))
            .Should().ThrowAsync<NotFoundException>().WithMessage("hidden");
        await FluentActions.Awaiting(() => handler.Handle(new GetHospitalRequestQuery(Guid.NewGuid(), LabMember), Cts.Token))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public void Mapper_ShowsBothNhsNumbers_WhenTheyDiffer()
    {
        var request = HospitalsTestConstants.Request(_hospital, _patient, Histology, Biopsy);

        var response = HospitalMapper.ToResponse(request, null, "test.lab");

        response.NhsNumbersDiffer.Should().BeTrue();
        response.LabCaseStatus.Should().Be("Open");
        response.Patient!.NhsNumber.Should().Be("9990000026");
        response.SpecimenTypeName.Should().Be(Biopsy.Name);
        HospitalMapper.ToSummary(request, LabCaseStatus.Closed).LabCaseStatus.Should().Be("Closed");
        HospitalMapper.ToMatch(_patient).IsRestricted.Should().BeFalse();
    }
}
