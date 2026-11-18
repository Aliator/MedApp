using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Contracts.Activity.Responses;
using MedApp.Contracts.Attachments.Responses;
using MedApp.Contracts.Common;
using MedApp.Contracts.Pathways.Timeline.Responses;
using MedApp.Contracts.Patients.Requests;
using MedApp.Contracts.Patients.Responses;
using MedApp.Contracts.Referrals.Requests;
using MedApp.Contracts.Referrals.Responses;
using MedApp.Contracts.Specialties.Requests;
using MedApp.Contracts.Specialties.Responses;
using MedApp.Domain.Specialties;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.IntegrationTests.Referrals;

/// <summary>
/// Referrals through the API against SQL Server: registering (the duplicate check, the restricted record's refusal),
/// editing from a version, withdrawing and marking entered in error, the letter as an attachment, the reads in the
/// activity log, the history on the timeline (free text for admins only), the export, and the rules that keep a
/// patient with referrals from being deleted or anonymised while one awaits triage.
/// </summary>
[TestFixture]
public sealed class ReferralTests : IntegrationTest
{
    private const string Referrals = "/api/referrals";
    private const string Reason = "Synthetic reason for referral: a changing mole.";
    private const string Practice = "Synthetic Lane Surgery";

    private HttpClient _admin = null!;

    [SetUp]
    public async Task SignInAsync() => _admin = await SignInAsAdminAsync();

    private static RegisterReferralRequest Request(Guid patientId, Guid? specialtyId = null, bool confirm = false) => new()
    {
        PatientId = patientId,
        SpecialtyId = specialtyId ?? Specialty.GeneralId,
        ReceivedOn = UkToday.AddDays(-3),
        ReferrerPractice = Practice,
        ReferrerClinician = "Dr A Synthetic",
        ReferrerOdsCode = "z99999",
        RequestedPriority = "Urgent",
        Reason = Reason,
        ConfirmPossibleDuplicate = confirm
    };

    private static async Task<ReferralResponse> RegisterAsync(HttpClient client, Guid patientId, bool confirm = false) =>
        await (await client.PostAsJsonAsync(Referrals, Request(patientId, confirm: confirm))).ReadAsAsync<ReferralResponse>(HttpStatusCode.Created);

    [Test]
    public async Task Register_ThenRead_GivesItsDetailsHistoryAndBanner()
    {
        var staff = await CreateStaffAsync("Nurse");
        var patient = await CreatePatientAsync(_admin);

        var registered = await RegisterAsync(staff.Client, patient.Id);
        var read = await (await staff.Client.GetAsync($"{Referrals}/{registered.Id}")).ReadAsAsync<ReferralResponse>();

        read.Status.Should().Be("AwaitingTriage");
        read.ReferrerOdsCode.Should().Be("Z99999");
        read.RegisteredByUsername.Should().Be(staff.Username);
        read.Patient!.Id.Should().Be(patient.Id);
        read.SpecialtyName.Should().Be(Specialty.GeneralName);
        read.Version.Should().Be(1);
        read.CanEdit.Should().BeTrue();
        read.History.Should().ContainSingle().Which.Action.Should().Be("Registered");
        read.ReceivedOn.Should().Be(UkToday.AddDays(-3));
    }

    [Test]
    public async Task Register_AnOpenOneToTheSameSpecialty_AsksForConfirmation()
    {
        var patient = await CreatePatientAsync(_admin);
        await RegisterAsync(_admin, patient.Id);

        var again = await _admin.PostAsJsonAsync(Referrals, Request(patient.Id));
        var problem = await again.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainKey("ConfirmPossibleDuplicate");
        problem.Errors["ConfirmPossibleDuplicate"].Single().Should().NotContain(Reason).And.NotContain(Practice);

        await RegisterAsync(_admin, patient.Id, confirm: true);
    }

    [Test]
    public async Task Register_RestrictedRecord_IsRefusedSayingToGiveTheLetterToAnAdmin()
    {
        var patient = await CreatePatientAsync(_admin);
        (await _admin.PutAsJsonAsync($"/api/patients/{patient.Id}/restriction", new SetPatientFlagRequest { Set = true, Reason = "Synthetic." }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _admin.PostAsJsonAsync(Referrals, Request(patient.Id));

        var problem = await response.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict);
        problem.Title.Should().Contain("Give the referral letter to an admin");
    }

    [Test]
    public async Task Edit_FromAnOldVersion_IsAConflict_AndTheHistoryNamesTheFieldsNotTheText()
    {
        var patient = await CreatePatientAsync(_admin);
        var referral = await RegisterAsync(_admin, patient.Id);
        var edit = new EditReferralRequest
        {
            SpecialtyId = referral.SpecialtyId, ReceivedOn = referral.ReceivedOn, ReferrerPractice = Practice,
            RequestedPriority = "High", Reason = "Synthetic corrected reason.", ExpectedVersion = 1
        };

        var edited = await (await _admin.PutAsJsonAsync($"{Referrals}/{referral.Id}", edit)).ReadAsAsync<ReferralResponse>();
        edited.Version.Should().Be(2);
        edited.History.Last().ChangedFields.Should().Contain(["reason", "requestedPriority", "referrerClinician", "referrerOdsCode"]);

        (await _admin.PutAsJsonAsync($"{Referrals}/{referral.Id}", edit)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var details = await RunInScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<MedApp.Infrastructure.Data.MedAppDbContext>();
            return await db.ReferralEvents.Where(e => e.ReferralId == referral.Id).Select(e => e.Details).ToListAsync();
        });
        details.Should().AllSatisfy(d => d.Should().NotContain("Synthetic corrected reason").And.NotContain(Reason));
    }

    [Test]
    public async Task WithdrawAndEnteredInError_CloseIt_AndAClosedOneCantChange()
    {
        var patient = await CreatePatientAsync(_admin);
        var first = await RegisterAsync(_admin, patient.Id);
        var second = await RegisterAsync(_admin, patient.Id, confirm: true);

        var withdrawn = await (await _admin.PostAsJsonAsync($"{Referrals}/{first.Id}/withdraw",
            new CloseReferralRequest { Detail = "Synthetic: the practice withdrew it.", ExpectedVersion = 1 })).ReadAsAsync<ReferralResponse>();
        withdrawn.Status.Should().Be("Withdrawn");
        withdrawn.DecisionDetail.Should().Be("Synthetic: the practice withdrew it.");

        var inError = await (await _admin.PostAsJsonAsync($"{Referrals}/{second.Id}/entered-in-error",
            new CloseReferralRequest { Detail = "Synthetic: registered twice.", ExpectedVersion = 1 })).ReadAsAsync<ReferralResponse>();
        inError.Status.Should().Be("EnteredInError");

        (await _admin.PostAsJsonAsync($"{Referrals}/{first.Id}/withdraw", new CloseReferralRequest { Detail = "Again.", ExpectedVersion = 2 }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        // Files can't be added to one entered in error.
        (await UploadAttachmentAsync(_admin, $"referralId={second.Id}", SyntheticPdf, "letter.pdf")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Withdraw_WhileRestricted_IsAllowed_ButEditingIsNot()
    {
        var patient = await CreatePatientAsync(_admin);
        var referral = await RegisterAsync(_admin, patient.Id);

        var restrict = await _admin.PutAsJsonAsync($"/api/patients/{patient.Id}/restriction", new SetPatientFlagRequest { Set = true, Reason = "Synthetic." });
        (await restrict.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest))
            .Errors["ConfirmOpenPathways"].Single().Should().Contain("1 referral awaiting triage");
        (await _admin.PutAsJsonAsync($"/api/patients/{patient.Id}/restriction",
            new SetPatientFlagRequest { Set = true, Reason = "Synthetic.", ConfirmOpenPathways = true })).StatusCode.Should().Be(HttpStatusCode.OK);

        var edit = new EditReferralRequest
        {
            SpecialtyId = referral.SpecialtyId, ReceivedOn = referral.ReceivedOn, ReferrerPractice = Practice,
            RequestedPriority = "High", Reason = Reason, ExpectedVersion = 1
        };
        (await _admin.PutAsJsonAsync($"{Referrals}/{referral.Id}", edit)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await UploadAttachmentAsync(_admin, $"referralId={referral.Id}", SyntheticPdf, "letter.pdf")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _admin.PostAsJsonAsync($"{Referrals}/{referral.Id}/withdraw", new CloseReferralRequest { Detail = "Synthetic.", ExpectedVersion = 1 }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Letter_IsAnAttachmentOnTheReferral_ListedWithThePatientsFiles()
    {
        var patient = await CreatePatientAsync(_admin);
        var referral = await RegisterAsync(_admin, patient.Id);

        var letter = await UploadAttachmentOkAsync(_admin, $"referralId={referral.Id}", SyntheticPdf, "Referral letter.pdf");

        letter.AttachedTo.Should().Be("Referral");
        letter.ReferralId.Should().Be(referral.Id);
        (await (await _admin.GetAsync($"/api/attachments?referralId={referral.Id}")).ReadAsAsync<AttachmentListResponse>())
            .Items.Should().ContainSingle(a => a.Id == letter.Id);
        (await (await _admin.GetAsync($"/api/attachments?patientId={patient.Id}")).ReadAsAsync<AttachmentListResponse>())
            .Items.Should().ContainSingle(a => a.Id == letter.Id);
        (await (await _admin.GetAsync($"{Referrals}/{referral.Id}")).ReadAsAsync<ReferralResponse>()).LetterCount.Should().Be(1);
        var page = await (await _admin.GetAsync($"{Referrals}/search?PatientId={patient.Id}"))
            .ReadAsAsync<PagedResponse<ReferralSummaryResponse>>();
        page.Items.Single().HasLetter.Should().BeTrue();
        (await _admin.GetAsync($"/api/attachments?referralId={Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Search_Inbox_OldestFirst_AndTheCountRecordsNothing()
    {
        var specialty = await (await _admin.PostAsJsonAsync("/api/specialties", new CreateSpecialtyRequest { Name = UniqueName("Specialty") }))
            .ReadAsAsync<SpecialtyResponse>(HttpStatusCode.Created);
        var newer = await CreatePatientAsync(_admin);
        var older = await CreatePatientAsync(_admin);
        await _admin.PostAsJsonAsync(Referrals, Request(newer.Id, specialty.Id));
        var olderRequest = Request(older.Id, specialty.Id);
        olderRequest.ReceivedOn = UkToday.AddDays(-30);
        await _admin.PostAsJsonAsync(Referrals, olderRequest);
        await ExecuteSqlAsync($"DELETE FROM ActivityEntries");

        var inbox = await (await _admin.GetAsync($"{Referrals}/search?Statuses=AwaitingTriage&SpecialtyIds={specialty.Id}"))
            .ReadAsAsync<PagedResponse<ReferralSummaryResponse>>();
        inbox.Items.Select(i => i.PatientId).Should().Equal(older.Id, newer.Id);
        inbox.Items[0].DaysSinceReceived.Should().Be(30);

        var count = await (await _admin.GetAsync($"{Referrals}/awaiting-triage/count")).ReadAsAsync<ReferralCountResponse>();
        count.Count.Should().BeGreaterThanOrEqualTo(2);

        var entries = (await (await _admin.GetAsync("/api/activity/search?PageSize=100&Actions=PatientListed"))
            .ReadAsAsync<PagedResponse<ActivityEntryResponse>>()).Items;
        entries.Select(e => e.PatientId).Should().BeEquivalentTo([older.Id, newer.Id]);

        // The specialty can't be archived while they wait, nor deleted at all.
        (await _admin.PostAsJsonAsync($"/api/specialties/{specialty.Id}/archive", new SpecialtyVersionRequest { ExpectedVersion = specialty.Version }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _admin.DeleteAsync($"/api/specialties/{specialty.Id}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Timeline_StaffSeeTheHistoryWithoutFreeText_AdminsWithIt()
    {
        var staff = await CreateStaffAsync("Nurse");
        var patient = await CreatePatientAsync(_admin);
        var referral = await RegisterAsync(_admin, patient.Id);
        await _admin.PostAsJsonAsync($"{Referrals}/{referral.Id}/withdraw",
            new CloseReferralRequest { Detail = "Synthetic withdrawal detail.", ExpectedVersion = 1 });

        var staffBody = await (await staff.Client.GetAsync($"/api/patients/{patient.Id}/timeline?Kinds=Referrals")).Content.ReadAsStringAsync();
        var adminPage = await (await _admin.GetAsync($"/api/patients/{patient.Id}/timeline")).ReadAsAsync<PatientTimelinePageResponse>();

        staffBody.Should().Contain("ReferralRegistered").And.Contain("ReferralWithdrawn")
            .And.NotContain(Reason).And.NotContain(Practice).And.NotContain("Synthetic withdrawal detail");
        adminPage.Items.Single(i => i.Action == "ReferralWithdrawn").Reason.Should().Be("Synthetic withdrawal detail.");
        adminPage.Items.Single(i => i.Action == "ReferralRegistered").Reason.Should().Be(Reason);
    }

    [Test]
    public async Task Patient_WithAReferral_CantBeDeleted_NorAnonymisedWhileItAwaitsTriage_AndTheExportHasIt()
    {
        var patient = await CreatePatientAsync(_admin);
        var referral = await RegisterAsync(_admin, patient.Id);

        (await _admin.DeleteAsync($"/api/patients/{patient.Id}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var anonymise = new AnonymisePatientRequest
        {
            Reason = "RetentionPeriodEnded", Confirmation = $"{patient.FirstName} {patient.LastName}"
        };
        (await _admin.PostAsJsonAsync($"/api/patients/{patient.Id}/anonymise", anonymise)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var export = await (await _admin.GetAsync($"/api/patients/{patient.Id}/export")).ReadAsAsync<PatientExportResponse>();
        export.Referrals.Single().Reason.Should().Be(Reason);
        export.ReferralHistory.Single().Action.Should().Be("Registered");

        await _admin.PostAsJsonAsync($"{Referrals}/{referral.Id}/withdraw", new CloseReferralRequest { Detail = "Synthetic.", ExpectedVersion = 1 });
        (await _admin.PostAsJsonAsync($"/api/patients/{patient.Id}/anonymise", anonymise)).StatusCode.Should().Be(HttpStatusCode.OK);
        var anonymised = await (await _admin.GetAsync($"{Referrals}/{referral.Id}")).ReadAsAsync<ReferralResponse>();
        anonymised.Reason.Should().NotBe(Reason);
        anonymised.ReferrerOdsCode.Should().BeNull();
    }

    [Test]
    public async Task WithoutRegisterReferrals_ChangesAreForbidden_ButReadingIsAllowed()
    {
        var reader = await CreateStaffAsync("Lab");
        var patient = await CreatePatientAsync(_admin);
        var referral = await RegisterAsync(_admin, patient.Id);

        (await reader.Client.PostAsJsonAsync(Referrals, Request(patient.Id, confirm: true))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var read = await (await reader.Client.GetAsync($"{Referrals}/{referral.Id}")).ReadAsAsync<ReferralResponse>();
        read.CanEdit.Should().BeFalse();
    }
}
