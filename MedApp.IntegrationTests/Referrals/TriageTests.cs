using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Referrals.Common;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Notifications.Responses;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Contracts.Patients.Requests;
using MedApp.Contracts.Referrals.Requests;
using MedApp.Contracts.Referrals.Responses;
using MedApp.Contracts.Specialties.Requests;
using MedApp.Contracts.Specialties.Responses;
using MedApp.Domain.Specialties;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.IntegrationTests.Referrals;

/// <summary>
/// Triage through the API against SQL Server: a Clinician in the referral's specialty accepts it into a pathway (one
/// save: the pathway, its audit and the referral), rejects it or redirects it; whoever registered it is told; two
/// triagers at once get one 409; the permission without the specialty is a 403; a restricted record is a 409; and the
/// Specialties list says how many members can triage.
/// </summary>
[TestFixture]
public sealed class TriageTests : IntegrationTest
{
    private const string Referrals = "/api/referrals";

    private HttpClient _admin = null!;
    private StaffAccount _booking = null!;

    [SetUp]
    public async Task SetUpAsync()
    {
        _admin = await SignInAsAdminAsync();
        _booking = await CreateStaffAsync("Booking");
    }

    /// <summary>A Clinician who belongs to these specialties (General when none are given).</summary>
    private async Task<StaffAccount> ClinicianInAsync(params Guid[] specialtyIds)
    {
        var clinician = await CreateStaffAsync("Clinician");
        (await _admin.PutAsJsonAsync(
                $"/api/auth/users/{clinician.Username}/specialties",
                new SetUserSpecialtiesRequest(specialtyIds.Length == 0 ? [Specialty.GeneralId] : specialtyIds)))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        return clinician;
    }

    private async Task<ReferralResponse> RegisterAsync(Guid patientId, Guid? specialtyId = null) =>
        await (await _booking.Client.PostAsJsonAsync(Referrals, new RegisterReferralRequest
        {
            PatientId = patientId,
            SpecialtyId = specialtyId ?? Specialty.GeneralId,
            ReceivedOn = UkToday.AddDays(-2),
            ReferrerPractice = "Synthetic Lane Surgery",
            RequestedPriority = "Urgent",
            Reason = "Synthetic reason for referral.",
        })).ReadAsAsync<ReferralResponse>(HttpStatusCode.Created);

    private static AcceptReferralRequest Accept(Guid templateId, int version = 1) => new()
    {
        TemplateId = templateId,
        Priority = "High",
        Notes = "Synthetic first note.",
        ExpectedVersion = version,
    };

    [Test]
    public async Task Accept_AsAClinicianInItsSpecialty_CreatesThePathwayAndTellsTheRegistrar()
    {
        var clinician = await ClinicianInAsync();
        var patient = await CreatePatientAsync(_admin);
        var template = await CreateTemplateWithStagesAsync(_admin, 2);
        var referral = await RegisterAsync(patient.Id);

        // The referral page offers the clinician triage, and the person who registered it not.
        (await (await clinician.Client.GetAsync($"{Referrals}/{referral.Id}")).ReadAsAsync<ReferralResponse>()).CanTriage.Should().BeTrue();
        (await (await _booking.Client.GetAsync($"{Referrals}/{referral.Id}")).ReadAsAsync<ReferralResponse>()).CanTriage.Should().BeFalse();

        var response = await clinician.Client.PostAsJsonAsync($"{Referrals}/{referral.Id}/accept", Accept(template.Id));

        var pathway = await response.ReadAsAsync<PatientPathwayResponse>(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().Be($"/api/pathways/patient-pathways/{pathway.Id}");
        pathway.PatientId.Should().Be(patient.Id);
        pathway.Priority.Should().Be("High");
        // The target date is the clock's: received two days ago, with High's 28 days (asked for: Urgent, 14).
        pathway.DueDate.Should().Be(UkToday.AddDays(26));
        pathway.Referral!.Id.Should().Be(referral.Id);

        var read = await (await clinician.Client.GetAsync($"{Referrals}/{referral.Id}")).ReadAsAsync<ReferralResponse>();
        read.Status.Should().Be("Accepted");
        read.PathwayId.Should().Be(pathway.Id);
        read.DecidedByUsername.Should().Be(clinician.Username);
        read.History.Single(h => h.Action == "Accepted").Should().BeEquivalentTo(new { Action = "Accepted", Priority = "High", TemplateName = template.Name, PathwayId = (Guid?)pathway.Id });

        // The pathway's own page links back to it, and its creation is in the audit like any new pathway's.
        var page = await (await clinician.Client.GetAsync($"/api/pathways/patient-pathways/{pathway.Id}")).ReadAsAsync<PatientPathwayResponse>();
        page.Referral.Should().BeEquivalentTo(new { referral.Id, ReceivedOn = UkToday.AddDays(-2), SpecialtyName = Specialty.GeneralName });
        page.NoteList.Should().ContainSingle();

        var told = (await (await _booking.Client.GetAsync("/api/notifications")).ReadAsAsync<NotificationsResponse>()).Items
            .Single(n => n.Kind == "ReferralTriaged");
        told.Title.Should().Be(ReferralNotifications.TriagedTitle);
        told.Message.Should().Be($"{clinician.Username} accepted a {Specialty.GeneralName} referral.");
        told.ReferralId.Should().Be(referral.Id);
    }

    [Test]
    public async Task Accept_TwoTriagersAtOnce_OneWinsAndTheOtherIsToldWho()
    {
        var first = await ClinicianInAsync();
        var second = await ClinicianInAsync();
        var patient = await CreatePatientAsync(_admin);
        var template = await CreateTemplateWithStagesAsync(_admin, 1);

        for (var i = 0; i < 3; i++)
        {
            // The one before was accepted into a pathway still open, or rejected: confirm it isn't a duplicate.
            var referral = await RegisterConfirmingAsync(patient.Id);

            var responses = await Task.WhenAll(
                first.Client.PostAsJsonAsync($"{Referrals}/{referral.Id}/accept", Accept(template.Id)),
                second.Client.PostAsJsonAsync($"{Referrals}/{referral.Id}/reject", new RejectReferralRequest { Reason = "Duplicate", ExpectedVersion = 1 }));

            var codes = responses.Select(r => r.StatusCode).ToList();
            codes.Should().HaveCount(2).And.Contain(HttpStatusCode.Conflict);
            codes.Should().ContainSingle(c => c == HttpStatusCode.Created || c == HttpStatusCode.OK);

            var loser = await responses.Single(r => r.StatusCode == HttpStatusCode.Conflict).ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict);
            var read = await (await _admin.GetAsync($"{Referrals}/{referral.Id}")).ReadAsAsync<ReferralResponse>();
            loser.Title.Should().Contain(read.DecidedByUsername!);
            // One decision after its registration, with what it did to the clock (a rejection stops it; an acceptance
            // at another priority re-reads its target), never both decisions' entries.
            string.Join(",", read.History.Select(h => h.Action)).Should().BeOneOf(
                "Registered,Rejected,ClockStopped", "Registered,Accepted,TargetChanged");

            // An acceptance that lost left no pathway behind.
            if (read.Status == "Rejected")
                read.PathwayId.Should().BeNull();
        }
    }

    private async Task<ReferralResponse> RegisterConfirmingAsync(Guid patientId) =>
        await (await _booking.Client.PostAsJsonAsync(Referrals, new RegisterReferralRequest
        {
            PatientId = patientId,
            SpecialtyId = Specialty.GeneralId,
            ReceivedOn = UkToday.AddDays(-2),
            ReferrerPractice = "Synthetic Lane Surgery",
            RequestedPriority = "Urgent",
            Reason = "Synthetic reason for referral.",
            ConfirmPossibleDuplicate = true,
        })).ReadAsAsync<ReferralResponse>(HttpStatusCode.Created);

    [Test]
    public async Task Triage_ThePermissionWithoutTheSpecialty_IsForbidden()
    {
        var other = await CreateSpecialtyAsync();
        var outsider = await ClinicianInAsync(other.Id);
        var patient = await CreatePatientAsync(_admin);
        var referral = await RegisterAsync(patient.Id);

        var response = await outsider.Client.PostAsJsonAsync(
            $"{Referrals}/{referral.Id}/reject", new RejectReferralRequest { Reason = "NotOfferedHere", ExpectedVersion = 1 });

        var problem = await response.ReadAsAsync<ProblemDetails>(HttpStatusCode.Forbidden);
        problem.Title.Should().Be(ReferralWorkflow.NotInSpecialtyMessage(Specialty.GeneralName));
        var read = await (await outsider.Client.GetAsync($"{Referrals}/{referral.Id}")).ReadAsAsync<ReferralResponse>();
        read.Status.Should().Be("AwaitingTriage");
        read.CanTriage.Should().BeFalse();
        read.WhyCantTriage.Should().Be(problem.Title);

        // An admin isn't a triager: Admin doesn't grant TriageReferrals.
        (await _admin.PostAsJsonAsync($"{Referrals}/{referral.Id}/reject", new RejectReferralRequest { Reason = "NotOfferedHere", ExpectedVersion = 1 }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Accept_OnARestrictedRecord_IsAConflictAndCreatesNothing()
    {
        var clinician = await ClinicianInAsync();
        var patient = await CreatePatientAsync(_admin);
        var template = await CreateTemplateWithStagesAsync(_admin, 1);
        var referral = await RegisterAsync(patient.Id);
        (await _admin.PutAsJsonAsync($"/api/patients/{patient.Id}/restriction",
                new SetPatientFlagRequest { Set = true, Reason = "Synthetic.", ConfirmOpenPathways = true }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await clinician.Client.PostAsJsonAsync($"{Referrals}/{referral.Id}/accept", Accept(template.Id));

        await response.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict);
        var pathways = await RunInScopeAsync(sp => Task.FromResult(
            sp.GetRequiredService<MedApp.Infrastructure.Data.MedAppDbContext>().PatientPathways.Count(p => p.PatientId == patient.Id)));
        pathways.Should().Be(0);
    }

    [Test]
    public async Task Accept_AnotherSpecialtysTemplate_IsABadRequest()
    {
        var clinician = await ClinicianInAsync();
        var patient = await CreatePatientAsync(_admin);
        var other = await CreateSpecialtyAsync();
        var template = await CreateTemplateWithStagesAsync(_admin, 1);
        (await _admin.PatchAsJsonAsync($"/api/pathways/patient-pathway-stage/templates/{template.Id}", new { specialtyId = other.Id }))
            .IsSuccessStatusCode.Should().BeTrue();
        var referral = await RegisterAsync(patient.Id);

        var response = await clinician.Client.PostAsJsonAsync($"{Referrals}/{referral.Id}/accept", Accept(template.Id));

        var problem = await response.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainKey("TemplateId");
    }

    [Test]
    public async Task Redirect_ToAnotherSpecialty_MovesItToThatInboxStillAwaitingTriage_ThenElsewhereClosesIt()
    {
        var other = await CreateSpecialtyAsync();
        var clinician = await ClinicianInAsync(Specialty.GeneralId, other.Id);
        var patient = await CreatePatientAsync(_admin);
        var referral = await RegisterAsync(patient.Id);

        var moved = await (await clinician.Client.PostAsJsonAsync($"{Referrals}/{referral.Id}/redirect",
            new RedirectReferralRequest { SpecialtyId = other.Id, Detail = "Synthetic: better seen there.", ExpectedVersion = 1 }))
            .ReadAsAsync<ReferralResponse>();

        moved.Status.Should().Be("AwaitingTriage");
        moved.SpecialtyName.Should().Be(other.Name);
        moved.RedirectDetail.Should().Be("Synthetic: better seen there.");
        moved.History.Single(h => h.Action == "SpecialtyChanged").Should().BeEquivalentTo(new { Action = "SpecialtyChanged", SpecialtyName = other.Name, PreviousSpecialtyName = Specialty.GeneralName });
        moved.CanTriage.Should().BeTrue("the clinician belongs to the new specialty too");

        var sent = await (await clinician.Client.PostAsJsonAsync($"{Referrals}/{referral.Id}/redirect",
            new RedirectReferralRequest { Elsewhere = "Synthetic Community Trust", Detail = "Synthetic: not offered here.", ExpectedVersion = 2 }))
            .ReadAsAsync<ReferralResponse>();

        sent.Status.Should().Be("RedirectedElsewhere");
        sent.RedirectedTo.Should().Be("Synthetic Community Trust");
        sent.DecisionDetail.Should().Be("Synthetic: not offered here.");
        var messages = (await (await _booking.Client.GetAsync("/api/notifications")).ReadAsAsync<NotificationsResponse>()).Items
            .Where(n => n.Kind == "ReferralTriaged").Select(n => n.Message);
        messages.Should().BeEquivalentTo(
        [
            $"{clinician.Username} redirected a {Specialty.GeneralName} referral to {other.Name}.",
            $"{clinician.Username} redirected a {other.Name} referral elsewhere.",
        ]);
    }

    [Test]
    public async Task Specialties_SayHowManyMembersCanTriage()
    {
        var nobody = await CreateSpecialtyAsync();
        await ClinicianInAsync(Specialty.GeneralId);
        var nurse = await CreateStaffAsync("Nurse");
        (await _admin.PutAsJsonAsync($"/api/auth/users/{nurse.Username}/specialties", new SetUserSpecialtiesRequest([nobody.Id])))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var specialties = await (await _admin.GetAsync("/api/specialties")).ReadAsAsync<List<SpecialtyResponse>>();

        specialties.Single(s => s.Id == Specialty.GeneralId).TriagerCount.Should().Be(1);
        var withoutTriager = specialties.Single(s => s.Id == nobody.Id);
        withoutTriager.MemberCount.Should().Be(1);
        withoutTriager.TriagerCount.Should().Be(0);
    }

    private async Task<SpecialtyResponse> CreateSpecialtyAsync() =>
        await (await _admin.PostAsJsonAsync("/api/specialties", new CreateSpecialtyRequest { Name = UniqueName("Specialty") }))
            .ReadAsAsync<SpecialtyResponse>(HttpStatusCode.Created);
}
