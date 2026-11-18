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

    private GetSpecimenHandler Handler => new(Repository.Object, _activity, Identity.Object, QrCodes.Object, Clock);

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
    private static SearchSpecimensQuery Query(string? view = null, Guid? labId = null, Guid? pathwayId = null) =>
        new(1, 50, view, labId, pathwayId, null);

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
        received.Laboratory = PartnerLab();
        received.LaboratoryId = received.Laboratory.Id;
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
        // The biopsy's 14 days: only the one received 20 days after it was taken is overdue.
        Run(Query("Overdue")).Should().Equal(SpecimenStatus.Received);
    }

    [Test]
    public void All_IsEverySpecimenNewestTakenFirst()
    {
        var all = Specimens().AsQueryable().Filter(Query(), Now).Sort(SpecimenView.All).ToList();

        all.Should().HaveCount(8);
        all.Select(s => s.TakenAtUtc).Should().BeInDescendingOrder();
    }

    [Test]
    public void LabFilter_KeepsThatLabs()
    {
        Run(Query("InLab", PartnerLab().Id)).Should().Equal(SpecimenStatus.Received);
        Run(Query("InLab", InHouseLab().Id)).Should().BeEmpty();
    }

    [Test]
    public void Validator_UnknownView_IsRefused()
    {
        new SearchSpecimensValidator().Validate(Query("Lunch")).IsValid.Should().BeFalse();
        new SearchSpecimensValidator().Validate(Query("inLab")).IsValid.Should().BeTrue();
        new SearchSpecimensValidator().Validate(new SearchSpecimensQuery(1, 101, null, null, null, null)).IsValid.Should().BeFalse();
    }

    [Test]
    public async Task Handle_RecordsEachPatientListed_AndMapsSummaries()
    {
        var specimens = Specimens();
        var activity = new RecordingActivityLog();
        var query = Query();
        Repository.Setup(r => r.SearchAsync(query, Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Specimen>(specimens, specimens.Count));

        var page = await new SearchSpecimensHandler(Repository.Object, activity, Clock).Handle(query, Cts.Token);

        page.Items.Should().HaveCount(8);
        page.Items.Single(i => i.Status == "Received").IsOverdueAtLab.Should().BeTrue();
        activity.Views.Should().ContainSingle(v => v.Action == ActivityAction.PatientListed)
            .Which.PatientIds.Should().BeEquivalentTo(specimens.Select(s => s.PatientId).Distinct());
    }
}
