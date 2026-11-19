using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Patients;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Paging;
using MedApp.Application.Specimens.Queries.GetSpecimen;
using MedApp.Application.Specimens.Queries.SearchSpecimens;
using MedApp.Domain.Activity;
using MedApp.Domain.Specimens;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using static MedApp.UnitTests.Common.Constants.SpecimensTestConstants;

namespace MedApp.UnitTests.Application.Specimens.Queries;

[TestFixture]
public sealed class GetSpecimenHandlerTests : SpecimenTestBase
{
    private readonly RecordingActivityLog _activity = new();

    private GetSpecimenHandler Handler => new(Repository.Object, _activity, Identity.Object, QrCodes.Object, Boundary.Object, Teams.Object, Clock);

    [SetUp]
    public void ClearActivity() => _activity.SingleViews.Clear();

    [Test]
    public async Task Handle_Get_RecordsTheViewWithTheSpecimensId_AndGivesTheLabel()
    {
        var specimen = Given(CreateSpecimen());

        var response = await Handler.Handle(new GetSpecimenQuery(specimen.Id, Nurse), Cts.Token);

        var view = _activity.SingleViews.Should().ContainSingle().Subject;
        view.Action.Should().Be(ActivityAction.SpecimenViewed);
        view.PatientId.Should().Be(specimen.PatientId);
        view.Details.Should().Contain(specimen.Id.ToString());
        response.Label.QrPath.Should().NotBeEmpty();
        response.RequestingUsername.Should().Be(Staff.Username);
        response.CanMarkSent.Should().BeTrue();
        response.CanMarkReceived.Should().BeFalse("the nurse doesn't process specimens");
    }

    [Test]
    public async Task Handle_Unknown_Throws404_AndRecordsNothing()
    {
        var act = () => Handler.Handle(new GetSpecimenQuery(Guid.NewGuid(), Nurse), Cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
        _activity.SingleViews.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_FindByNumber_IgnoresCaseAndSpaces_AndRecordsTheView()
    {
        var specimen = Given(CreateSpecimen());

        var response = await Handler.Handle(new FindSpecimenQuery(" ma26-000 123 ", LabStaff), Cts.Token);

        response.Id.Should().Be(specimen.Id);
        _activity.SingleViews.Should().ContainSingle(v => v.Action == ActivityAction.SpecimenViewed);
    }

    [Test]
    public async Task Handle_FindUnknownNumber_Throws404()
    {
        var act = () => Handler.Handle(new FindSpecimenQuery("MA26-999999", LabStaff), Cts.Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage(GetSpecimenHandler.NotFoundByNumberMessage);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("MA26-0000000000000000")]
    public void FindValidator_NoNumberOrTooLong_IsRefused(string number)
    {
        new FindSpecimenValidator().Validate(new FindSpecimenQuery(number, LabStaff)).IsValid.Should().BeFalse();
    }
}

[TestFixture]
public sealed class SearchSpecimensTests : SpecimenTestBase
{
    private static SearchSpecimensQuery Query(string? view = null, Guid? pathwayId = null) =>
        new(1, 50, view, pathwayId, null);

    /// <summary>One of each status, at different times, the LINQ run in memory as EF runs it in SQL.</summary>
    private static List<Specimen> Specimens()
    {
        var taken = CreateSpecimen(takenAtUtc: Now.AddDays(-2));
        var sent = CreateSpecimen(status: SpecimenStatus.Sent, takenAtUtc: Now.AddDays(-3));
        var received = CreateSpecimen(status: SpecimenStatus.Received, takenAtUtc: Now.AddDays(-20));
        var reported = CreateSpecimen(status: SpecimenStatus.Reported, takenAtUtc: Now.AddDays(-4));
        var oldReport = CreateSpecimen(status: SpecimenStatus.Reported, takenAtUtc: Now.AddDays(-10));
        var rejected = CreateSpecimen(status: SpecimenStatus.RejectedByLab, takenAtUtc: Now.AddDays(-5));
        var lostLongAgo = CreateSpecimen(status: SpecimenStatus.Lost, takenAtUtc: Now.AddDays(-40));
        var error = CreateSpecimen(status: SpecimenStatus.EnteredInError, takenAtUtc: Now.AddDays(-1));
        received.LabTest = Histology();
        received.LabTestId = received.LabTest.Id;
        return [taken, sent, received, reported, oldReport, rejected, lostLongAgo, error];
    }

    private static List<SpecimenStatus> Run(SearchSpecimensQuery query) =>
        Specimens().AsQueryable().Filter(query, Now).Sort(query.ParsedView).Select(s => s.Status).ToList();

    [Test]
    public void Views_HoldTheirStatusesOldestFirst()
    {
        Run(Query("ToSend")).Should().Equal(SpecimenStatus.Taken);
        Run(Query("Expected")).Should().Equal(SpecimenStatus.Sent);
        Run(Query("InLab")).Should().Equal(SpecimenStatus.Received);
        Run(Query("RejectedAndLost")).Should().Equal(SpecimenStatus.RejectedByLab);
        Run(Query("Reported")).Should().Equal(SpecimenStatus.Reported);
    }

    [Test]
    public void Overdue_IsOpenPastTheTypesExpectedDays()
    {
        // The biopsy's 14 days: only the one received 20 days after it was taken is overdue (worked out in memory).
        var query = Query("Overdue");
        query.NeedsClock.Should().BeTrue();
        SpecimenSearch.Arrange(Specimens().AsQueryable().Filter(query, Now), query, Now).Select(s => s.Status)
            .Should().Equal(SpecimenStatus.Received);
    }

    [Test]
    public void All_IsEverySpecimenNewestTakenFirst()
    {
        var all = Specimens().AsQueryable().Filter(Query(), Now).Sort(SpecimenView.All).ToList();

        all.Should().HaveCount(8);
        all.Select(s => s.TakenAtUtc).Should().BeInDescendingOrder();
    }

    [Test]
    public void Filters_KeepTheTestStatusSourceAndClinicAsked()
    {
        Run(Query() with { LabTestId = Histology().Id }).Should().Equal(SpecimenStatus.Received);
        Run(Query() with { Status = "sent" }).Should().Equal(SpecimenStatus.Sent);
        Run(Query() with { Source = "Hospital" }).Should().BeEmpty();
        Run(Query() with { Source = "Clinic" }).Should().HaveCount(8);
        Run(Query() with { TeamId = Guid.NewGuid() }).Should().BeEmpty();
    }

    [Test]
    public void ToReceive_IsTakenOrSent_OldestTakenFirst()
    {
        Run(Query("ToReceive")).Should().Equal(SpecimenStatus.Sent, SpecimenStatus.Taken);
    }

    [Test]
    public void DueFilter_FollowsTheExpectedDate_InMemory_OpenOnesOnly()
    {
        var overdue = Query() with { Due = "Overdue" };
        var notDue = Query() with { Due = "NotDue" };

        SpecimenSearch.Arrange(Specimens().AsQueryable().Filter(overdue, Now), overdue, Now).Select(s => s.Status)
            .Should().Equal(SpecimenStatus.Received);
        SpecimenSearch.Arrange(Specimens().AsQueryable().Filter(notDue, Now), notDue, Now).Select(s => s.Status)
            .Should().BeEquivalentTo([SpecimenStatus.Taken, SpecimenStatus.Sent]);
    }

    [Test]
    public void Validator_UnknownSourceStatusOrDue_IsRefused()
    {
        new SearchSpecimensValidator().Validate(Query() with { Source = "Moon" }).IsValid.Should().BeFalse();
        new SearchSpecimensValidator().Validate(Query() with { Status = "Lunch" }).IsValid.Should().BeFalse();
        new SearchSpecimensValidator().Validate(Query() with { Due = "Later" }).IsValid.Should().BeFalse();
        new SearchSpecimensValidator().Validate(Query() with { Due = "dueSoon", Source = "hospital", Status = "Taken" }).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validator_UnknownView_IsRefused()
    {
        new SearchSpecimensValidator().Validate(Query("Lunch")).IsValid.Should().BeFalse();
        new SearchSpecimensValidator().Validate(Query("inLab")).IsValid.Should().BeTrue();
        new SearchSpecimensValidator().Validate(new SearchSpecimensQuery(1, 101, null, null, null)).IsValid.Should().BeFalse();
    }

    [Test]
    public async Task Handle_RecordsEachPatientListed_AndMapsSummaries()
    {
        var specimens = Specimens();
        var activity = new RecordingActivityLog();
        var query = Query();
        Repository.Setup(r => r.SearchAsync(query, Lab.Id, Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Specimen>(specimens, specimens.Count));

        var page = await new SearchSpecimensHandler(Repository.Object, Teams.Object, activity, Clock).Handle(query, Cts.Token);

        page.Items.Should().HaveCount(8);
        page.Items.Single(i => i.Status == "Received").IsOverdueAtLab.Should().BeTrue();
        activity.Views.Should().ContainSingle(v => v.Action == ActivityAction.PatientListed)
            .Which.PatientIds.Should().BeEquivalentTo(specimens.Select(s => s.PatientId).Distinct());
    }

    /// <summary>A sample with its own number, patient, type and test, for the samples page's search and sort.</summary>
    private static Specimen Named(string number, string first, string last, string? nhsNumber, SpecimenType type, MedApp.Domain.Lab.LabTest? test, SpecimenStatus status, int daysAgo)
    {
        var patient = new Patient { Id = Guid.NewGuid(), FirstName = first, LastName = last, NhsNumber = nhsNumber };
        var template = CreateSpecimen(CreatePathway(PatientPathwayStatus.InProgress, patient: patient), status, Now.AddDays(-daysAgo));
        return new Specimen
        {
            Id = template.Id,
            Number = number,
            PatientId = patient.Id,
            Patient = patient,
            PathwayId = template.PathwayId,
            Pathway = template.Pathway,
            StageId = template.StageId,
            SpecimenType = type,
            SpecimenTypeId = type.Id,
            LabTest = test,
            LabTestId = test?.Id,
            Source = SpecimenSource.Clinic,
            Status = status,
            TakenAtUtc = template.TakenAtUtc,
            SentAtUtc = template.SentAtUtc,
            ReceivedAtUtc = template.ReceivedAtUtc,
            ReportedAtUtc = template.ReportedAtUtc,
            ClosedAtUtc = template.ClosedAtUtc,
        };
    }

    private static List<Specimen> Worklist() =>
    [
        Named("MA26-000101", "Ada", "Zephyr", "9990000026", Biopsy(), Histology(), SpecimenStatus.Taken, 3),
        Named("MA26-000102", "Bea", "Young", null, Urine(), Bloods(), SpecimenStatus.Sent, 1),
        Named("MA26-000103", "Cal", "Xu", null, Biopsy(), new MedApp.Domain.Lab.LabTest { Id = Guid.NewGuid(), Name = "Allergy screen" }, SpecimenStatus.Received, 2),
    ];

    private static List<string> Numbers(SearchSpecimensQuery query) =>
        Worklist().AsQueryable().Filter(query, Now).Sort(query.ParsedView, query.Sort, query.Ascending).Select(s => s.Number).ToList();

    private static SearchSpecimensQuery Searched(string? search, string? sort = null, bool ascending = true, string? view = null) =>
        new(1, 50, view, null, null, search, sort, ascending);

    [TestCase("ma26-000102", "MA26-000102")]
    [TestCase(" MA26 - 000103 ", "MA26-000103")]
    [TestCase("bea young", "MA26-000102")]
    [TestCase("ZEPHYR", "MA26-000101")]
    [TestCase("999 000 0026", "MA26-000101")]
    [TestCase("urine", "MA26-000102")]
    [TestCase("allergy", "MA26-000103")]
    public void Search_FindsTheNumberWithoutSpaces_ThePatientsNameOrNhsNumber_TheTypeOrTheTest(string search, string number)
    {
        Numbers(Searched(search)).Should().Equal(number);
    }

    [Test]
    public void Search_NothingMatching_FindsNothing()
    {
        Numbers(Searched("MA26-999999")).Should().BeEmpty();
        Numbers(Searched("   ")).Should().HaveCount(3);
    }

    [TestCase("number", true, "MA26-000101", "MA26-000102", "MA26-000103")]
    [TestCase("patient", true, "MA26-000103", "MA26-000102", "MA26-000101")]
    [TestCase("PATIENT", false, "MA26-000101", "MA26-000102", "MA26-000103")]
    [TestCase("test", true, "MA26-000103", "MA26-000102", "MA26-000101")]
    [TestCase("status", false, "MA26-000103", "MA26-000102", "MA26-000101")]
    [TestCase("time", true, "MA26-000101", "MA26-000103", "MA26-000102")]
    [TestCase("site", true, "MA26-000102", "MA26-000103", "MA26-000101")]
    public void Sort_ByAColumnInEitherDirection_OrTheViewsOwnOrder(string sort, bool ascending, params string[] numbers)
    {
        Numbers(Searched(null, sort, ascending)).Should().Equal(numbers);
    }

    [TestCase("Expected")]
    [TestCase("InLab")]
    [TestCase("Overdue")]
    [TestCase("RejectedAndLost")]
    [TestCase("Reported")]
    [TestCase("ToSend")]
    public void Sort_ByTime_UsesTheViewsOwnTime(string view)
    {
        var all = Specimens();
        var query = Searched(null, "time", false, view);
        var expected = all.AsQueryable().Filter(query, Now).Sort(query.ParsedView).Select(s => s.Id).Reverse().ToList();

        all.AsQueryable().Filter(query, Now).Sort(query.ParsedView, "time", false).Select(s => s.Id).Should().Equal(expected);
    }

    [Test]
    public void Validator_ASearchOver200Characters_IsRefused()
    {
        new SearchSpecimensValidator().Validate(Searched(new string('x', 201))).IsValid.Should().BeFalse();
        new SearchSpecimensValidator().Validate(Searched(new string('x', 200))).IsValid.Should().BeTrue();
    }
}
