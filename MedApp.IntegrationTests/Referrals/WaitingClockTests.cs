using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Common;
using MedApp.Contracts.Pathways.PatientPathways.Requests;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Requests;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MedApp.Contracts.Patients.Requests;
using MedApp.Contracts.Referrals.Requests;
using MedApp.Contracts.Referrals.Responses;
using MedApp.Contracts.Teams.Requests;
using MedApp.Contracts.Teams.Responses;
using MedApp.Domain.Teams;
using MedApp.IntegrationTests.Common;

namespace MedApp.IntegrationTests.Referrals;

/// <summary>
/// The waiting-time clock through the API against SQL Server: the accepted pathway's target date is the clock's; a
/// pause (Workers only) moves it to the review date, and resuming moves it back, later by the days paused; one open
/// pause at a time; the target date can't be edited by hand while the clock sets it; the clock-stop stage and a
/// cancellation stop it; a restricted record can be paused but not resumed; and the lists filter and sort by the
/// clock in SQL (days waited, breached, paused, review date).
/// </summary>
[TestFixture]
public sealed class WaitingClockTests : IntegrationTest
{
    private const string Referrals = "/api/referrals";

    private HttpClient _admin = null!;
    private StaffAccount _booking = null!;
    private StaffAccount _clinician = null!;

    [SetUp]
    public async Task SetUpAsync()
    {
        _admin = await SignInAsAdminAsync();
        _booking = await CreateStaffAsync("Booking");
        _clinician = await CreateStaffAsync("Clinician");
        (await _admin.PutAsJsonAsync($"/api/auth/users/{_clinician.Username}/teams", new SetUserTeamsRequest([new(Team.GeneralId, null)])))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<ReferralResponse> RegisterAsync(Guid patientId, int receivedDaysAgo = 10, Guid? teamId = null) =>
        await (await _booking.Client.PostAsJsonAsync(Referrals, new RegisterReferralRequest
        {
            PatientId = patientId,
            TeamId = teamId ?? Team.GeneralId,
            ReceivedOn = UkToday.AddDays(-receivedDaysAgo),
            ReferrerPractice = "Synthetic Lane Surgery",
            RequestedPriority = "High",
            Reason = "Synthetic reason for referral.",
        })).ReadAsAsync<ReferralResponse>(HttpStatusCode.Created);

    /// <summary>A pathway accepted from a referral received <paramref name="receivedDaysAgo"/> days ago, High (28 days).</summary>
    private async Task<(ReferralResponse Referral, PatientPathwayResponse Pathway)> AcceptedAsync(
        int receivedDaysAgo = 10, int? clockStopStageOrder = null)
    {
        var patient = await CreatePatientAsync(_admin);
        var template = await CreateTemplateWithStagesAsync(_admin, 2);
        if (clockStopStageOrder is { } order)
            template = await (await _admin.PatchAsJsonAsync($"/api/pathways/patient-pathway-stage/templates/{template.Id}",
                new UpdatePathwayTemplateRequest { StageDefinitionIdsInOrder = template.StageDefinitionIdsInOrder, ClockStopStageOrder = order }))
                .ReadAsAsync<PathwayTemplateResponse>();
        var referral = await RegisterAsync(patient.Id, receivedDaysAgo);
        var pathway = await (await _clinician.Client.PostAsJsonAsync($"{Referrals}/{referral.Id}/accept",
                new AcceptReferralRequest { TemplateId = template.Id, Priority = "High", ExpectedVersion = referral.Version }))
            .ReadAsAsync<PatientPathwayResponse>(HttpStatusCode.Created);
        return (referral, pathway);
    }

    private async Task<PatientPathwayResponse> PathwayAsync(Guid id) =>
        await (await _clinician.Client.GetAsync(PathwayUrl(id))).ReadAsAsync<PatientPathwayResponse>();

    private static Task<HttpResponseMessage> PauseAsync(HttpClient client, Guid referralId, int version, int reviewInDays = 30) =>
        client.PostAsJsonAsync($"{Referrals}/{referralId}/clock/pause", new PauseClockRequest
        {
            Reason = "PatientChoseToWait", Detail = "Synthetic: away until the spring.", ReviewOn = UkToday.AddDays(reviewInDays),
            ExpectedVersion = version
        });

    [Test]
    public async Task Accept_TheTargetDateIsTheClocks()
    {
        var (referral, pathway) = await AcceptedAsync(receivedDaysAgo: 10);

        pathway.DueDate.Should().Be(UkToday.AddDays(18));
        var page = await PathwayAsync(pathway.Id);
        page.Referral!.Clock!.Should().BeEquivalentTo(new { State = "Running", TargetDays = 28, DaysWaited = 10, IsBreached = false });
        page.Referral.CanPauseClock.Should().BeTrue();
        page.Referral.Version.Should().Be(2);
        (await (await _clinician.Client.GetAsync($"{Referrals}/{referral.Id}")).ReadAsAsync<ReferralResponse>())
            .Clock!.TargetOn.Should().Be(UkToday.AddDays(18));
    }

    [Test]
    public async Task PauseAndResume_MoveTheTargetDateAndCountOnlyTheDaysNotPaused()
    {
        var (referral, pathway) = await AcceptedAsync(receivedDaysAgo: 10);

        // Booking can't work on pathways, so can't pause; a clinician can.
        (await PauseAsync(_booking.Client, referral.Id, 2)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var paused = await (await PauseAsync(_clinician.Client, referral.Id, 2)).ReadAsAsync<PathwayReferralResponse>();

        paused.Clock!.State.Should().Be("Paused");
        paused.Clock.PauseReviewOn.Should().Be(UkToday.AddDays(30));
        paused.Pauses.Single().Detail.Should().Be("Synthetic: away until the spring.");
        (await PathwayAsync(pathway.Id)).DueDate.Should().Be(UkToday.AddDays(30));

        // One open pause at a time; and the target date isn't edited by hand while the clock sets it.
        (await PauseAsync(_clinician.Client, referral.Id, paused.Version)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _clinician.Client.PatchAsJsonAsync(PathwayUrl(pathway.Id), new UpdatePatientPathwayRequest { DueDate = UkToday.AddDays(5) }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        Clock.Advance(TimeSpan.FromDays(4));
        var resumed = await (await _clinician.Client.PostAsJsonAsync($"{Referrals}/{referral.Id}/clock/resume",
            new ResumeClockRequest { ExpectedVersion = paused.Version })).ReadAsAsync<PathwayReferralResponse>();

        resumed.Clock!.Should().BeEquivalentTo(new { State = "Running", PausedDays = 4, DaysWaited = 10 });
        (await PathwayAsync(pathway.Id)).DueDate.Should().Be(UkToday.AddDays(18));

        var history = (await (await _clinician.Client.GetAsync($"{Referrals}/{referral.Id}")).ReadAsAsync<ReferralResponse>()).History;
        history.Select(h => h.Action).Should().ContainInOrder("Accepted", "ClockPaused", "ClockResumed");
        history.Single(h => h.Action == "ClockPaused").ReviewOn.Should().Be(UkToday.AddDays(26));
    }

    [Test]
    public async Task Pause_FromAnOldVersion_IsAConflict_AndAReviewDateTooFarIsABadRequest()
    {
        var (referral, _) = await AcceptedAsync();

        (await PauseAsync(_clinician.Client, referral.Id, 1)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await PauseAsync(_clinician.Client, referral.Id, 2, reviewInDays: 91)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ClockStopStage_Finished_StopsTheClockForGood_AndTheTargetDateCanBeEditedAgain()
    {
        var (referral, pathway) = await AcceptedAsync(clockStopStageOrder: 1);
        var stage = pathway.Stages.OrderBy(s => s.StageOrder).First();

        (await FinishStageAsync(_clinician.Client, pathway.Id, stage.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await PathwayAsync(pathway.Id);
        page.Referral!.Clock!.Should().BeEquivalentTo(new { State = "Stopped", StopReason = "StopStageFinished", StoppedOn = (DateOnly?)UkToday });
        page.Referral.CanPauseClock.Should().BeFalse();
        (await PauseAsync(_clinician.Client, referral.Id, page.Referral.Version)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _clinician.Client.PatchAsJsonAsync(PathwayUrl(pathway.Id), new UpdatePatientPathwayRequest { DueDate = UkToday.AddDays(5) }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Cancel_WhilePaused_StopsTheClockAndEndsThePause()
    {
        var (referral, pathway) = await AcceptedAsync();
        await (await PauseAsync(_clinician.Client, referral.Id, 2)).ReadAsAsync<PathwayReferralResponse>();
        Clock.Advance(TimeSpan.FromDays(3));

        (await PostPathwayStatusAsync(_clinician.Client, pathway.Id, "Cancelled", "Synthetic: no longer needed."))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var read = await (await _clinician.Client.GetAsync($"{Referrals}/{referral.Id}")).ReadAsAsync<ReferralResponse>();
        read.Clock!.Should().BeEquivalentTo(new { State = "Stopped", StopReason = "PathwayCancelled", PausedDays = 3, DaysWaited = 10 });
        read.Pauses.Single().ResumedOn.Should().Be(UkToday);
    }

    [Test]
    public async Task Restricted_PausingIsAllowed_ResumingIsAConflict()
    {
        var (referral, pathway) = await AcceptedAsync();
        (await _admin.PutAsJsonAsync($"/api/patients/{pathway.PatientId}/restriction",
                new SetPatientFlagRequest { Set = true, Reason = "Synthetic restriction reason.", ConfirmOpenPathways = true }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var paused = await (await PauseAsync(_clinician.Client, referral.Id, 2)).ReadAsAsync<PathwayReferralResponse>();

        paused.CanResumeClock.Should().BeFalse();
        (await _clinician.Client.PostAsJsonAsync($"{Referrals}/{referral.Id}/clock/resume", new ResumeClockRequest { ExpectedVersion = paused.Version }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Lists_FilterAndSortByTheClockInSql()
    {
        var (longWait, longPathway) = await AcceptedAsync(receivedDaysAgo: 40);
        var (shortWait, shortPathway) = await AcceptedAsync(receivedDaysAgo: 2);
        var (pausedReferral, _) = await AcceptedAsync(receivedDaysAgo: 5);
        await (await PauseAsync(_clinician.Client, pausedReferral.Id, 2, reviewInDays: 3)).ReadAsAsync<PathwayReferralResponse>();

        var byWait = await (await _clinician.Client.GetAsync(
                "/api/pathways/patient-pathways/search?sort=daysWaited&ascending=false&pageSize=100"))
            .ReadAsAsync<PagedResponse<PatientPathwaySummaryResponse>>();
        byWait.Items.Select(i => i.Id).Should().ContainInOrder(longPathway.Id, shortPathway.Id);
        byWait.Items.First().Clock!.IsBreached.Should().BeTrue();

        var breached = await (await _clinician.Client.GetAsync("/api/pathways/patient-pathways/search?breachedOnly=true"))
            .ReadAsAsync<PagedResponse<PatientPathwaySummaryResponse>>();
        breached.Items.Select(i => i.Id).Should().Equal(longPathway.Id);

        var paused = await (await _clinician.Client.GetAsync($"{Referrals}/search?paused=true&sort=reviewOn"))
            .ReadAsAsync<PagedResponse<ReferralSummaryResponse>>();
        paused.Items.Select(i => i.Id).Should().Equal(pausedReferral.Id);
        paused.Items.Single().Clock!.PauseReviewOn.Should().Be(UkToday.AddDays(3));

        var waiting = await (await _clinician.Client.GetAsync(
                $"{Referrals}/search?statuses=Accepted&clockStopped=false&sort=daysWaited&ascending=false&breachedOnly=false"))
            .ReadAsAsync<PagedResponse<ReferralSummaryResponse>>();
        waiting.Items.Select(i => i.Id).Should().Equal(longWait.Id, pausedReferral.Id, shortWait.Id);
    }

    [Test]
    public async Task TeamTargets_AreCopiedByNewClocks()
    {
        var created = await (await _admin.PostAsJsonAsync("/api/teams",
                new CreateTeamRequest { Name = UniqueName("Team"), TargetDaysHigh = 21 }))
            .ReadAsAsync<TeamResponse>(HttpStatusCode.Created);
        created.Should().BeEquivalentTo(new { TargetDaysUrgent = 14, TargetDaysHigh = 21, TargetDaysMedium = 126, TargetDaysLow = 126 });
        var patient = await CreatePatientAsync(_admin);

        var referral = await RegisterAsync(patient.Id, receivedDaysAgo: 1, teamId: created.Id);

        referral.Clock!.TargetDays.Should().Be(21);
        referral.Clock.TargetOn.Should().Be(UkToday.AddDays(20));
    }

    [Test]
    public async Task SkippedClockStopStage_StopsNothingUntilDischarge_AndReinstatingNeverRestartsIt()
    {
        var patient = await CreatePatientAsync(_admin);
        var template = await CreateTemplateWithStagesAsync(_admin, 3);
        // The second stage stops the clock, and is optional.
        template = await (await _admin.PatchAsJsonAsync($"/api/pathways/patient-pathway-stage/templates/{template.Id}",
                new UpdatePathwayTemplateRequest
                {
                    StageDefinitionIdsInOrder = template.StageDefinitionIdsInOrder,
                    ClockStopStageOrder = 2,
                    StageOptions = [new(), new() { IsOptional = true }, new()],
                }))
            .ReadAsAsync<PathwayTemplateResponse>();
        var referral = await RegisterAsync(patient.Id);
        var pathway = await (await _clinician.Client.PostAsJsonAsync($"{Referrals}/{referral.Id}/accept",
                new AcceptReferralRequest { TemplateId = template.Id, Priority = "High", ExpectedVersion = referral.Version }))
            .ReadAsAsync<PatientPathwayResponse>(HttpStatusCode.Created);
        var stages = pathway.Stages;

        await (await FinishStageAsync(_clinician.Client, pathway.Id, stages[0].Id)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await _clinician.Client.PostAsJsonAsync($"{StageUrl(pathway.Id, stages[1].Id)}/skip",
            new SkipPathwayStageRequest { Reason = "NotNeeded" })).ShouldHaveStatusAsync(HttpStatusCode.OK);
        (await PathwayAsync(pathway.Id)).Referral!.Clock!.State.Should().Be("Running");

        await (await FinishStageAsync(_clinician.Client, pathway.Id, stages[2].Id)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await PostPathwayStatusAsync(_clinician.Client, pathway.Id, "Completed")).ShouldHaveStatusAsync(HttpStatusCode.OK);
        (await PathwayAsync(pathway.Id)).Referral!.Clock!.Should().BeEquivalentTo(new { State = "Stopped", StopReason = "PathwayDischarged" });

        var reinstated = await (await _admin.PostAsJsonAsync($"{StageUrl(pathway.Id, stages[1].Id)}/reinstate",
            new ReinstatePathwayStageRequest { Reason = "Synthetic: needed after all." })).ReadAsAsync<PatientPathwayResponse>();
        reinstated.Status.Should().Be("InProgress");
        (await PathwayAsync(pathway.Id)).Referral!.Clock!.Should().BeEquivalentTo(new { State = "Stopped", StopReason = "PathwayDischarged" });
    }
}
