using MedApp.UnitTests.Common.Constants;
using MedApp.Domain.Patients;
using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Paging;
using MedApp.Application.Letters.Common;
using MedApp.Application.Letters.Queries.GetLetter;
using MedApp.Application.Letters.Queries.SearchLetters;
using MedApp.Application.Letters.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Letters;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.LettersTestConstants;

namespace MedApp.UnitTests.Application.Letters.Queries;

[TestFixture]
public sealed class LetterQueriesTests : LetterHandlerTestBase
{
    private readonly CancellationTokenSource _cts = new();

    [OneTimeTearDown]
    public void TearDown() => _cts.Dispose();

    [Test]
    public async Task Get_RecordsTheView_AndGivesTheViewersFlags()
    {
        var letter = Given(OnPathway(LetterStatus.AwaitingApproval));
        letter.Body = "{{patient.name}} at {{practice.name}}, {{referral.receivedDate}}";

        var response = await Fixture.Create<GetLetterHandler>().Handle(new GetLetterQuery(letter.Id, Approver), _cts.Token);

        Activity.SingleViews.Should().ContainSingle();
        Activity.SingleViews.Single().Action.Should().Be(ActivityAction.LetterViewed);
        Activity.SingleViews.Single().PatientId.Should().Be(Patient.Id);
        Activity.SingleViews.Single().Details.Should().Contain(letter.Id.ToString());
        response.CanApprove.Should().BeTrue();
        response.CanReturn.Should().BeTrue();
        response.CanEdit.Should().BeFalse();
        response.CanPreview.Should().BeTrue();
        response.MissingMergeFields.Should().Equal("referral.receivedDate");
        response.Patient!.Id.Should().Be(Patient.Id);
        response.PathwayTitle.Should().Be(Pathway.Title);
        response.AuthorUsername.Should().Be(Author.Username);
        response.ApproverUsername.Should().Be(Approver.Username);
        response.SecureEmailAvailable.Should().BeTrue();
    }

    [Test]
    public async Task Get_AnUnknownLetter_IsNotFound()
    {
        var act = () => Fixture.Create<GetLetterHandler>().Handle(new GetLetterQuery(Guid.NewGuid(), Approver), _cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
        Activity.SingleViews.Should().BeEmpty();
    }

    [Test]
    public async Task Preview_RendersTheDraftMarkedAsADraft_AndRecordsTheView()
    {
        var letter = Given(OnPathway());

        var pdf = await Fixture.Create<PreviewLetterHandler>().Handle(new PreviewLetterQuery(letter.Id, Author), _cts.Token);

        pdf.Content.Should().Equal(Pdf);
        pdf.FileName.Should().Be(PreviewLetterHandler.FileName);
        Renderer.Verify(r => r.RenderAsync(It.Is<LetterDocument>(d => d.Marking == LetterRendering.DraftMarking), _cts.Token));
        Activity.SingleViews.Single().Details.Should().Contain("\"preview\":true");
    }

    [Test]
    public async Task Preview_BySomeoneElse_IsForbidden_AndOfAnApprovedLetter_AConflict()
    {
        var draft = Given(OnPathway());
        var approved = Given(OnPathway(LetterStatus.Approved));

        await FluentActions.Awaiting(() => Fixture.Create<PreviewLetterHandler>().Handle(new PreviewLetterQuery(draft.Id, Sender), _cts.Token))
            .Should().ThrowAsync<ForbiddenException>();
        await FluentActions.Awaiting(() => Fixture.Create<PreviewLetterHandler>().Handle(new PreviewLetterQuery(approved.Id, Approver), _cts.Token))
            .Should().ThrowAsync<ConflictException>();
        Activity.SingleViews.Should().BeEmpty();
    }

    [Test]
    public async Task Count_GivesTheRepositorysCounts()
    {
        Letters.Setup(l => l.CountAsync(Approver.UserId, MedApp.Application.Common.Teams.TeamScope.System, _cts.Token)).ReturnsAsync((2, 5));

        var count = await Fixture.Create<CountLettersHandler>().Handle(new CountLettersQuery(Approver.UserId), _cts.Token);

        count.ToApprove.Should().Be(2);
        count.ToSend.Should().Be(5);
    }

    [Test]
    public async Task Search_RecordsEachPatientListed_AndMapsTheRows()
    {
        var letter = OnPathway(LetterStatus.Approved);
        var query = new SearchLettersQuery(1, 10, "ToSend", null, null, null, Approver.UserId);
        Letters.Setup(l => l.SearchAsync(query, _cts.Token)).ReturnsAsync(
            new PagedResult<LetterRow>([new LetterRow(letter, "Sam", "Example", Pathway.Title)], 1));

        var page = await Fixture.Create<SearchLettersHandler>().Handle(query, _cts.Token);

        Activity.Views.Should().ContainSingle();
        Activity.Views.Single().Action.Should().Be(ActivityAction.PatientListed);
        Activity.Views.Single().PatientIds.Should().Equal(Patient.Id);
        page.TotalCount.Should().Be(1);
        page.Items.Single().Should().BeEquivalentTo(new
        {
            PatientName = "Sam Example", PathwayTitle = Pathway.Title, Status = "Approved", RecipientCount = 2, UnsentCount = 2,
            AuthorUsername = Author.Username, ApproverUsername = Approver.Username,
        });
    }

    [Test]
    public void SearchValidator_NeedsAViewOrAFilter_AndAKnownView()
    {
        var validator = new SearchLettersValidator();

        validator.Validate(new SearchLettersQuery(1, 10, null, null, null, null, Approver.UserId)).IsValid.Should().BeFalse();
        validator.Validate(new SearchLettersQuery(1, 10, "Everything", null, null, null, Approver.UserId)).IsValid.Should().BeFalse();
        validator.Validate(new SearchLettersQuery(0, 101, "Sent", null, null, null, Approver.UserId)).Errors.Should().HaveCount(2);
        validator.Validate(new SearchLettersQuery(1, 10, null, Patient.Id, null, null, Approver.UserId)).IsValid.Should().BeTrue();
        validator.Validate(new SearchLettersQuery(1, 10, "MyDrafts", null, null, null, Approver.UserId)).IsValid.Should().BeTrue();
    }

    [Test]
    public void LetterSearch_EachView_FiltersAndOrdersAsTheListShowsIt()
    {
        var mine = OnPathway(author: Author);
        mine.LastUpdated = NowUtc;
        var mineOlder = OnPathway(author: Author);
        mineOlder.LastUpdated = NowUtc.AddDays(-1);
        var returned = OnPathway(author: Author);
        returned.ReturnComment = "x";
        returned.ReturnedAtUtc = NowUtc;
        var someoneElses = OnPathway(author: ClinicianAuthor);
        var awaiting = OnPathway(LetterStatus.AwaitingApproval);
        var toSend = OnPathway(LetterStatus.Approved);
        var sent = OnPathway(LetterStatus.Approved);
        sent.Recipients.ForEach(r => r.DispatchStatus = LetterDispatchStatus.Sent);
        var withdrawn = OnPathway(LetterStatus.Withdrawn);
        var all = new[] { mine, mineOlder, returned, someoneElses, awaiting, toSend, sent, withdrawn }.AsQueryable();

        IEnumerable<Letter> View(string? view, Guid viewer) =>
            LetterSearch.Apply(all, new SearchLettersQuery(1, 10, view, null, null, null, viewer));

        View("MyDrafts", Author.UserId).Should().Equal(mine, mineOlder);
        View("ReturnedToMe", Author.UserId).Should().Equal(returned);
        View("AwaitingMyApproval", Approver.UserId).Should().Equal(awaiting);
        View("AwaitingMyApproval", Author.UserId).Should().BeEmpty();
        View("ToSend", Author.UserId).Should().Equal(toSend);
        View("Sent", Author.UserId).Should().Equal(sent);
        LetterSearch.Apply(all, new SearchLettersQuery(1, 10, null, null, Pathway.Id, null, Author.UserId)).Should().HaveCount(8);
        LetterSearch.Apply(all, new SearchLettersQuery(1, 10, null, null, null, Guid.NewGuid(), Author.UserId)).Should().BeEmpty();
        LetterSearch.Apply(all, new SearchLettersQuery(1, 10, null, Guid.NewGuid(), null, null, Author.UserId)).Should().BeEmpty();
    }

    [Test]
    public void LetterSearch_All_SearchesThePatientsName_AndSortsByAColumn()
    {
        var other = new Patient { Id = Guid.NewGuid(), FirstName = "Zed", LastName = "Abbott" };
        var draft = OnPathway();
        draft.LastUpdated = NowUtc.AddDays(-2);
        var approved = OnPathway(LetterStatus.Approved, kind: LetterKind.DischargeSummary);
        var othersLetter = CreateLetter(PathwaysTestConstants.CreatePathway(patient: other), Practice, LetterStatus.AwaitingApproval);
        var withdrawn = OnPathway(LetterStatus.Withdrawn);
        withdrawn.WithdrawnAtUtc = NowUtc;
        var all = new[] { draft, approved, othersLetter, withdrawn }.AsQueryable();
        var patients = new[] { Patient, other }.AsQueryable();

        List<Letter> Run(string? search = null, string? sort = null, bool ascending = true, string? view = "All") =>
            LetterSearch.Apply(all, new SearchLettersQuery(1, 10, view, null, null, null, Author.UserId, search, sort, ascending), patients).ToList();

        Run().Should().HaveCount(4);
        Run("zed ABB").Should().Equal(othersLetter);
        Run($"{Patient.FirstName.ToUpperInvariant()}").Should().BeEquivalentTo(new[] { draft, approved, withdrawn });
        // Without the patients, the name isn't searched or sorted by.
        LetterSearch.Apply(all, new SearchLettersQuery(1, 10, "All", null, null, null, Author.UserId, "zed")).Should().HaveCount(4);

        Run(sort: "patient").First().Should().Be(othersLetter);
        Run(sort: "patient", ascending: false).Last().Should().Be(othersLetter);
        Run(sort: "letter", ascending: false).First().Should().Be(approved);
        Run(sort: "status").Should().Equal(draft, othersLetter, approved, withdrawn);
        Run(sort: "latest", ascending: false).Should().Equal(withdrawn, approved, draft, othersLetter);
        Run(sort: "author").Should().HaveCount(4);
        Run(sort: "patient", view: "MyDrafts").Should().Equal(draft);
    }

    [Test]
    public void SearchValidator_ASearchOver200Characters_IsRefused()
    {
        var validator = new SearchLettersValidator();
        validator.Validate(new SearchLettersQuery(1, 10, "All", null, null, null, Author.UserId, new string('x', 201))).IsValid.Should().BeFalse();
        validator.Validate(new SearchLettersQuery(1, 10, "All", null, null, null, Author.UserId, "Freya", "latest", false)).IsValid.Should().BeTrue();
    }
}
