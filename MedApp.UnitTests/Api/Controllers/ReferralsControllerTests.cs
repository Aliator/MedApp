using AutoFixture;
using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Common.Identity;
using MedApp.Application.Referrals.Commands.AcceptReferral;
using MedApp.Application.Referrals.Commands.EditReferral;
using MedApp.Application.Referrals.Commands.MarkReferralEnteredInError;
using MedApp.Application.Referrals.Commands.RedirectReferral;
using MedApp.Application.Referrals.Commands.RegisterReferral;
using MedApp.Application.Referrals.Commands.RejectReferral;
using MedApp.Application.Referrals.Commands.WithdrawReferral;
using MedApp.Application.Referrals.Queries.CountReferralsAwaitingTriage;
using MedApp.Application.Referrals.Queries.GetReferralById;
using MedApp.Application.Referrals.Queries.SearchReferrals;
using MedApp.Contracts.Common;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Contracts.Referrals.Requests;
using MedApp.Contracts.Referrals.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;
using static MedApp.UnitTests.Common.Constants.ReferralsTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class ReferralsControllerTests
{
    private static readonly string[] Changes =
    [
        nameof(ReferralsController.RegisterReferral),
        nameof(ReferralsController.EditReferral),
        nameof(ReferralsController.WithdrawReferral),
        nameof(ReferralsController.MarkEnteredInError),
    ];

    private static readonly string[] Triage =
    [
        nameof(ReferralsController.AcceptReferral),
        nameof(ReferralsController.RejectReferral),
        nameof(ReferralsController.RedirectReferral),
    ];

    private IFixture _fixture = null!;
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _cts = new CancellationTokenSource();
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    private static ReferralsController Controller(RecordingMediator mediator) =>
        new ReferralsController(mediator).WithRequest(SignedIn("User"));

    private async Task ExpectOk(object response, Func<object, bool> request, Func<ReferralsController, Task<IActionResult>> act)
    {
        var mediator = new RecordingMediator(_ => response);

        var result = await act(Controller(mediator));

        request(mediator.Request!).Should().BeTrue(mediator.Request!.ToString());
        mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
    }

    [Test]
    public async Task RegisterReferral_SendsEveryFieldAsTheCallerAndReturnsItsAddress()
    {
        var referral = _fixture.Create<ReferralResponse>();
        var mediator = new RecordingMediator(_ => referral);
        var request = new RegisterReferralRequest
        {
            PatientId = RecordId, SpecialtyId = OtherId, ReceivedOn = ReceivedOn, ReferrerPractice = Practice,
            ReferrerClinician = Clinician, ReferrerOdsCode = OdsCode, RequestedPriority = "Urgent", Reason = Reason,
            ConfirmPossibleDuplicate = true
        };

        var result = await Controller(mediator).RegisterReferral(request, _cts.Token);

        var command = mediator.Request.Should().BeOfType<RegisterReferralCommand>().Subject;
        command.Should().BeEquivalentTo(new
        {
            PatientId = RecordId, SpecialtyId = OtherId, ReceivedOn, ReferrerPractice = Practice, ReferrerClinician = Clinician,
            ReferrerOdsCode = OdsCode, RequestedPriority = "Urgent", Reason, ConfirmPossibleDuplicate = true
        });
        command.Actor.UserId.Should().Be(UserId);
        mediator.Tokens.Single().Should().Be(_cts.Token);
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/referrals/{referral.Id}");
        created.Value.Should().Be(referral);
    }

    [Test]
    public async Task SearchReferrals_SendsTheFiltersAndTheViewer() =>
        await ExpectOk(new PagedResponse<ReferralSummaryResponse>([], 2, 20, 0, 0),
            r => r is SearchReferralsQuery { Page: 2, PageSize: 20, Sort: "patient", Ascending: false, Mine: true } q
                 && q.Statuses!.SequenceEqual(["AwaitingTriage"]) && q.SpecialtyIds!.SequenceEqual([OtherId])
                 && q.PatientId == RecordId && q.Search == "syn" && q.ViewerId == UserId,
            c => c.SearchReferrals(new SearchReferralsRequest
            {
                Page = 2, PageSize = 20, Sort = "patient", Ascending = false, Statuses = ["AwaitingTriage"],
                SpecialtyIds = [OtherId], Mine = true, PatientId = RecordId, Search = "syn"
            }, _cts.Token));

    [Test]
    public async Task CountAwaitingTriage_AsksForTheCallersCount() =>
        await ExpectOk(new ReferralCountResponse { Count = 3 },
            r => r is CountReferralsAwaitingTriageQuery q && q.ViewerId == UserId,
            c => c.CountAwaitingTriage(_cts.Token));

    [Test]
    public async Task GetReferral_ReturnsItForTheCaller() =>
        await ExpectOk(_fixture.Create<ReferralResponse>(),
            r => r is GetReferralByIdQuery q && q.ReferralId == RecordId && q.Viewer.UserId == UserId,
            c => c.GetReferral(RecordId, _cts.Token));

    [Test]
    public async Task EditReferral_SendsTheDetailsAndTheVersionSeen() =>
        await ExpectOk(_fixture.Create<ReferralResponse>(),
            r => r is EditReferralCommand { ExpectedVersion: 4, Reason: Reason, RequestedPriority: "Low" } q
                 && q.ReferralId == RecordId && q.SpecialtyId == OtherId && q.Actor.UserId == UserId,
            c => c.EditReferral(RecordId, new EditReferralRequest
            {
                SpecialtyId = OtherId, ReceivedOn = ReceivedOn, ReferrerPractice = Practice, RequestedPriority = "Low",
                Reason = Reason, ExpectedVersion = 4
            }, _cts.Token));

    [Test]
    public async Task WithdrawAndEnteredInError_SendTheDetailAndTheVersionSeen()
    {
        var response = _fixture.Create<ReferralResponse>();
        var request = new CloseReferralRequest { Detail = Detail, ExpectedVersion = 2 };
        await ExpectOk(response,
            r => r is WithdrawReferralCommand { Detail: Detail, ExpectedVersion: 2 } q && q.ReferralId == RecordId,
            c => c.WithdrawReferral(RecordId, request, _cts.Token));
        await ExpectOk(response,
            r => r is MarkReferralEnteredInErrorCommand { Detail: Detail, ExpectedVersion: 2 } q && q.ReferralId == RecordId,
            c => c.MarkEnteredInError(RecordId, request, _cts.Token));
    }

    [Test]
    public async Task AcceptReferral_SendsEveryChoiceAndTheVersionSeen_AndReturnsThePathwaysAddress()
    {
        var pathway = _fixture.Create<PatientPathwayResponse>();
        var mediator = new RecordingMediator(_ => pathway);
        var request = new AcceptReferralRequest
        {
            TemplateId = OtherId, Priority = "Urgent", DueDate = ReceivedOn, Title = "Synthetic title", AssigneeUserId = UserId,
            Notes = "Synthetic note", ExpectedVersion = 3
        };

        var result = await Controller(mediator).AcceptReferral(RecordId, request, _cts.Token);

        var command = mediator.Request.Should().BeOfType<AcceptReferralCommand>().Subject;
        command.Should().BeEquivalentTo(new
        {
            ReferralId = RecordId, TemplateId = OtherId, Priority = "Urgent", DueDate = ReceivedOn, Title = "Synthetic title",
            AssigneeUserId = (Guid?)UserId, Notes = "Synthetic note", ExpectedVersion = 3
        });
        command.Actor.UserId.Should().Be(UserId);
        mediator.Tokens.Single().Should().Be(_cts.Token);
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/pathways/patient-pathways/{pathway.Id}");
        created.Value.Should().Be(pathway);
    }

    [Test]
    public async Task RejectAndRedirect_SendTheChoiceAndTheVersionSeen()
    {
        var response = _fixture.Create<ReferralResponse>();
        await ExpectOk(response,
            r => r is RejectReferralCommand { Reason: "Duplicate", Detail: Detail, ExpectedVersion: 2 } q
                 && q.ReferralId == RecordId && q.Actor.UserId == UserId,
            c => c.RejectReferral(RecordId, new RejectReferralRequest { Reason = "Duplicate", Detail = Detail, ExpectedVersion = 2 }, _cts.Token));
        await ExpectOk(response,
            r => r is RedirectReferralCommand { Elsewhere: "Synthetic Trust", Detail: Detail, ExpectedVersion: 2 } q
                 && q.ReferralId == RecordId && q.SpecialtyId == OtherId && q.Actor.UserId == UserId,
            c => c.RedirectReferral(RecordId, new RedirectReferralRequest
            {
                SpecialtyId = OtherId, Elsewhere = "Synthetic Trust", Detail = Detail, ExpectedVersion = 2
            }, _cts.Token));
    }

    [Test]
    public async Task Triage_NeedsTriageReferrals()
    {
        foreach (var action in Triage)
        {
            (await ControllerAuthorisation.MayCallAsync<ReferralsController>(action, SignedInWith(Permission.TriageReferrals))).Should().BeTrue(action);
            (await ControllerAuthorisation.MayCallAsync<ReferralsController>(action, SignedInWith(Permission.RegisterReferrals))).Should().BeFalse(action);
            (await ControllerAuthorisation.MayCallAsync<ReferralsController>(action, SignedIn())).Should().BeFalse(action);
        }
    }

    [Test]
    public async Task Changes_NeedRegisterReferrals()
    {
        foreach (var action in Changes)
        {
            (await ControllerAuthorisation.MayCallAsync<ReferralsController>(action, SignedInWith(Permission.RegisterReferrals))).Should().BeTrue(action);
            (await ControllerAuthorisation.MayCallAsync<ReferralsController>(action, SignedInWith(Permission.ViewPatients))).Should().BeFalse(action);
            (await ControllerAuthorisation.MayCallAsync<ReferralsController>(action, SignedIn())).Should().BeFalse(action);
        }
    }

    [Test]
    public async Task Reading_NeedsViewPatients()
    {
        var reads = ControllerAuthorisation.Actions<ReferralsController>().Except(Changes).Except(Triage).ToList();
        reads.Should().BeEquivalentTo([
            nameof(ReferralsController.SearchReferrals),
            nameof(ReferralsController.CountAwaitingTriage),
            nameof(ReferralsController.GetReferral)]);

        foreach (var action in reads)
        {
            (await ControllerAuthorisation.MayCallAsync<ReferralsController>(action, SignedInWith(Permission.ViewPatients))).Should().BeTrue(action);
            (await ControllerAuthorisation.MayCallAsync<ReferralsController>(action, SignedInWith(Permission.UseApp))).Should().BeFalse(action);
        }
    }
}
