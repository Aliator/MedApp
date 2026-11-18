using MedApp.Domain.Patients;
using FluentAssertions;
using MedApp.Application.Common.Paging;
using MedApp.Application.Results.Queries.GetResult;
using MedApp.Application.Results.Queries.SearchResults;
using MedApp.Domain.Activity;
using MedApp.Domain.Results;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using static MedApp.UnitTests.Common.Constants.ResultsTestConstants;

namespace MedApp.UnitTests.Application.Results.Queries;

[TestFixture]
public sealed class ResultQueriesTests : ResultTestBase
{
    private static SearchResultsQuery Query(string? view, Guid? specialtyId = null, Guid? viewer = null) =>
        new(1, 10, view, specialtyId, null, null, viewer ?? Clinician.UserId);

    private static List<Result> Results()
    {
        var mineOld = CreateResult(reportedAtUtc: Now.AddDays(-5));
        var mineNew = CreateResult(status: ResultStatus.Seen, reportedAtUtc: Now.AddDays(-1));
        var others = CreateResult(reportedAtUtc: Now.AddDays(-3));
        others.ResponsibleUserId = OtherClinician.UserId;
        var orphan = CreateResult(reportedAtUtc: Now.AddDays(-4));
        orphan.ResponsibleUserId = OtherUserId;
        var signedOld = CreateResult(status: ResultStatus.SignedOff, reportedAtUtc: Now.AddDays(-9));
        signedOld.SignedOffAtUtc = Now.AddDays(-8);
        var signedNew = CreateResult(status: ResultStatus.SignedOff, reportedAtUtc: Now.AddDays(-7));
        signedNew.SignedOffAtUtc = Now.AddDays(-2);
        var error = CreateResult(status: ResultStatus.EnteredInError, reportedAtUtc: Now.AddDays(-6));
        return [mineOld, mineNew, others, orphan, signedOld, signedNew, error];
    }

    private static List<Result> Search(List<Result> all, SearchResultsQuery query) =>
        all.AsQueryable().Filter(query, SignOffHolders).Sort(query.ParsedView).ToList();

    [Test]
    public void Views_FilterAndOrderByDatesOnly()
    {
        var all = Results();

        Search(all, Query("AwaitingMine")).Should().Equal(all[0], all[1]);
        Search(all, Query("Awaiting")).Should().Equal(all[0], all[3], all[2], all[1]);
        Search(all, Query("NeedsClinician")).Should().Equal(all[3]);
        Search(all, Query("SignedOff")).Should().Equal(all[5], all[4]);
        Search(all, Query(null)).Should().HaveCount(7).And.BeInDescendingOrder(r => r.ReportedAtUtc);
    }

    [Test]
    public void SpecialtyFilter_KeepsOnlyThatSpecialtysPathways()
    {
        var all = Results();
        all[0].Pathway!.SpecialtyId = Guid.NewGuid();

        Search(all, Query("AwaitingMine", specialtyId: all[0].Pathway!.SpecialtyId)).Should().Equal(all[0]);
    }

    [Test]
    public async Task Search_RecordsEachPatientListed_AndFlagsNeedsAClinician()
    {
        var all = Results();
        var log = new RecordingActivityLog();
        Repository
            .Setup(r => r.SearchAsync(It.IsAny<SearchResultsQuery>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Result>([all[3]], 1));

        var page = await new SearchResultsHandler(Repository.Object, Holders.Object, Identity.Object, log, Clock)
            .Handle(Query("NeedsClinician"), Cts.Token);

        page.Items.Should().ContainSingle().Which.NeedsClinician.Should().BeTrue();
        log.Views.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Action = ActivityAction.PatientListed, PatientIds = new List<Guid> { all[3].PatientId },
        }, o => o.ExcludingMissingMembers());
    }

    [Test]
    public async Task Count_IsMineAwaiting()
    {
        Repository.Setup(r => r.CountAwaitingForAsync(Clinician.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(4);

        var count = await new SearchResultsHandler(Repository.Object, Holders.Object, Identity.Object, new RecordingActivityLog(), Clock)
            .Handle(new CountResultsQuery(Clinician.UserId), Cts.Token);

        count.AwaitingMine.Should().Be(4);
    }

    [Test]
    public async Task Get_RecordsResultViewedWithItsId()
    {
        var result = Given(CreateResult());
        var log = new RecordingActivityLog();

        var response = await new GetResultHandler(Repository.Object, log, Identity.Object, Holders.Object, Clock)
            .Handle(new GetResultQuery(result.Id, Clinician), Cts.Token);

        response.Id.Should().Be(result.Id);
        var view = log.SingleViews.Should().ContainSingle().Subject;
        view.Action.Should().Be(ActivityAction.ResultViewed);
        view.PatientId.Should().Be(result.PatientId);
        view.Details.Should().Contain(result.Id.ToString());
    }

    [Test]
    public void Validator_UnknownView_IsRefused()
    {
        new SearchResultsValidator().Validate(Query("Abnormal")).Errors.Should().ContainSingle(e => e.PropertyName == "View");
    }

    /// <summary>Three results awaiting sign-off, with their own patients, titles and pathways (one without).</summary>
    private static List<Result> Named()
    {
        var ada = CreateResult(reportedAtUtc: Now.AddDays(-3));
        ada.Patient = new Patient { Id = ada.PatientId, FirstName = "Ada", LastName = "Zephyr" };
        ada.Title = "Bloods";
        ada.Pathway!.Title = "Knee";
        var bea = CreateResult(status: ResultStatus.Seen, reportedAtUtc: Now.AddDays(-1));
        bea.Patient = new Patient { Id = bea.PatientId, FirstName = "Bea", LastName = "Young" };
        bea.Title = "Culture";
        bea.Pathway!.Title = "Ankle";
        var cal = CreateResult(reportedAtUtc: Now.AddDays(-2));
        cal.Patient = new Patient { Id = cal.PatientId, FirstName = "Cal", LastName = "Xu" };
        cal.Title = "Anaemia screen";
        cal.Pathway = null;
        return [ada, bea, cal];
    }

    private static List<string> Patients(SearchResultsQuery query) =>
        Named().AsQueryable().Filter(query, SignOffHolders).Sort(query.ParsedView, query.Sort, query.Ascending).Select(r => r.Patient.FirstName).ToList();

    private static SearchResultsQuery Searched(string? search, string? sort = null, bool ascending = true) =>
        new(1, 10, "Awaiting", null, null, null, Clinician.UserId, search, sort, ascending);

    [TestCase("bea young", "Bea")]
    [TestCase("XU", "Cal")]
    public void Search_FindsThePatientsName_IgnoringCase(string search, string first)
    {
        Patients(Searched(search)).Should().Equal(first);
    }

    [Test]
    public void Search_NeverLooksInTheTitle()
    {
        Patients(Searched("bloods")).Should().BeEmpty();
    }

    [TestCase("patient", true, "Cal", "Bea", "Ada")]
    [TestCase("title", true, "Cal", "Ada", "Bea")]
    [TestCase("pathway", true, "Cal", "Bea", "Ada")]
    [TestCase("reported", false, "Bea", "Cal", "Ada")]
    [TestCase("status", false, "Bea", "Ada", "Cal")]
    [TestCase("content", true, "Ada", "Cal", "Bea")]
    public void Sort_ByAColumnInEitherDirection_OrTheViewsOwnOrder(string sort, bool ascending, params string[] names)
    {
        var order = Patients(Searched(null, sort, ascending));

        // Ada and Cal both await sign-off, so a status sort fixes only Bea (Seen) first.
        if (sort == "status")
            order.First().Should().Be(names[0]);
        else
            order.Should().Equal(names);
    }

    [Test]
    public void Validator_ASearchOver200Characters_IsRefused()
    {
        new SearchResultsValidator().Validate(Searched(new string('x', 201))).IsValid.Should().BeFalse();
        new SearchResultsValidator().Validate(Searched("Freya", "title")).IsValid.Should().BeTrue();
    }
}
