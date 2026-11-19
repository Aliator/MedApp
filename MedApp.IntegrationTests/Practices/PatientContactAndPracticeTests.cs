using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Contracts.Activity.Responses;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Contracts.Common;
using MedApp.Contracts.Fhir;
using MedApp.Contracts.Patients.Requests;
using MedApp.Contracts.Patients.Responses;
using MedApp.Contracts.Practices.Requests;
using MedApp.Contracts.Practices.Responses;
using MedApp.Contracts.Referrals.Requests;
using MedApp.Contracts.Referrals.Responses;
using MedApp.Domain.Teams;
using MedApp.IntegrationTests.Common;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Practices;

/// <summary>
/// The GP practice directory, patients' contact details and registered practice, referrals' practice from the directory,
/// and staff full names and job titles, through the API against SQL Server: the unique ODS code and the foreign keys
/// that keep a practice in use from being deleted, the activity log's field names, the export, anonymisation and FHIR.
/// Synthetic values only: ZZ postcodes, Ofcom's drama range for mobiles, invented practices.
/// </summary>
[TestFixture]
public sealed class PatientContactAndPracticeTests : IntegrationTest
{
    private const string Practices = "/api/practices";
    private const string AddressLine = "1 Imaginary Lane";
    private const string Town = "Fictionton";
    private const string Postcode = "ZZ1 1ZZ";
    private const string Mobile = "+447700900123";

    private HttpClient _admin = null!;

    [SetUp]
    public async Task SignInAsync() => _admin = await SignInAsAdminAsync();

    private static string UniqueOdsCode() => "Z" + Random.Shared.Next(100000, 999999);

    private static async Task<PracticeResponse> CreatePracticeAsync(HttpClient client, string? odsCode = null) =>
        await (await (await ClinicalAsync(client)).PostAsJsonAsync(Practices, new CreatePracticeRequest
            {
                Name = UniqueName("Testbrook Surgery"),
                OdsCode = odsCode,
                AddressLines = ["2 Madeup Road", Town],
                Postcode = "zz2 2zz",
                Phone = "01632 960000",
                SecureEmail = "testbrook.surgery@nhs.net"
            }))
            .ReadAsAsync<PracticeResponse>(HttpStatusCode.Created);

    private async Task<PatientResponse> CreatePatientWithContactDetailsAsync(Guid practiceId) =>
        await (await (await ClinicalAsync(_admin)).PostAsJsonAsync("/api/patients", new CreatePatientRequest
            {
                FirstName = UniqueName("First"),
                LastName = UniqueName("Last"),
                DateOfBirth = DateOfBirth,
                Email = $"{Guid.NewGuid():N}@{PatientEmailDomain}",
                AddressLines = [AddressLine, Town],
                Postcode = "zz11zz",
                MobilePhone = "07700 900123",
                PracticeId = practiceId
            }))
            .ReadAsAsync<PatientResponse>(HttpStatusCode.Created);

    [Test]
    public async Task Directory_AddsEditsArchivesRestoresAndDeletes_WithTheOdsCodeUnique()
    {
        var staff = await CreateStaffAsync("Booking");
        var code = UniqueOdsCode();
        var created = await CreatePracticeAsync(staff.Client, code.ToLowerInvariant());
        created.Should().BeEquivalentTo(new
        {
            OdsCode = code, Postcode = "ZZ2 2ZZ", Version = 1, CanBeDeleted = true, IsArchived = false
        });
        created.AddressLines.Should().Equal("2 Madeup Road", Town);

        // Another practice with the code is refused; an email outside nhs.net too.
        await (await staff.Client.PostAsJsonAsync(Practices, new CreatePracticeRequest { Name = UniqueName("Other"), OdsCode = code }))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await (await staff.Client.PostAsJsonAsync(Practices, new CreatePracticeRequest { Name = UniqueName("Other"), SecureEmail = "a@example.org" }))
            .ShouldHaveStatusAsync(HttpStatusCode.BadRequest);

        var renamed = await (await staff.Client.PutAsJsonAsync($"{Practices}/{created.Id}", new UpdatePracticeRequest
            {
                Name = "Renamed Synthetic Surgery", OdsCode = code, AddressLines = [AddressLine], ExpectedVersion = 1
            }))
            .ReadAsAsync<PracticeResponse>();
        renamed.Should().BeEquivalentTo(new { Name = "Renamed Synthetic Surgery", Version = 2, Postcode = (string?)null });

        // A page still on version 1 is told someone changed it.
        await (await staff.Client.PostAsJsonAsync($"{Practices}/{created.Id}/archive", new PracticeVersionRequest { ExpectedVersion = 1 }))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        var archived = await (await staff.Client.PostAsJsonAsync($"{Practices}/{created.Id}/archive", new PracticeVersionRequest { ExpectedVersion = 2 }))
            .ReadAsAsync<PracticeResponse>();
        archived.IsArchived.Should().BeTrue();
        (await (await staff.Client.GetAsync(Practices)).ReadAsAsync<List<PracticeResponse>>()).Should().NotContain(p => p.Id == created.Id);
        (await (await staff.Client.GetAsync($"{Practices}?includeArchived=true")).ReadAsAsync<List<PracticeResponse>>())
            .Should().Contain(p => p.Id == created.Id);

        await (await staff.Client.PostAsJsonAsync($"{Practices}/{created.Id}/restore", new PracticeVersionRequest { ExpectedVersion = 3 }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await staff.Client.DeleteAsync($"{Practices}/{created.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        await (await staff.Client.GetAsync($"{Practices}/{created.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Directory_ReadingNeedsViewPatients_ChangingNeedsEditPatients()
    {
        var lab = await CreateStaffAsync("Lab");
        var practice = await CreatePracticeAsync(_admin);

        await (await lab.Client.GetAsync($"{Practices}/{practice.Id}")).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await lab.Client.PostAsJsonAsync(Practices, new CreatePracticeRequest { Name = UniqueName("Lab's") }))
            .ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await (await NewClient().GetAsync(Practices)).ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Patient_ContactDetailsAndPractice_AreSavedLoggedByNameExportedAndShownInFhir()
    {
        var practice = await CreatePracticeAsync(_admin, UniqueOdsCode());
        var patient = await CreatePatientWithContactDetailsAsync(practice.Id);

        patient.Should().BeEquivalentTo(new { Postcode, MobilePhone = Mobile, PracticeId = practice.Id });
        patient.AddressLines.Should().Equal(AddressLine, Town);
        patient.Practice!.Name.Should().Be(practice.Name);

        var read = await (await _admin.GetAsync($"/api/patients/{patient.Id}")).ReadAsAsync<PatientResponse>();
        read.Practice!.OdsCode.Should().Be(practice.OdsCode);
        (await GetPracticeAsync(practice.Id)).Should().BeEquivalentTo(new { PatientCount = 1, CanBeDeleted = false });

        // An edit records the names of the fields changed, never their values.
        await (await (await ClinicalAsync(_admin)).PatchAsJsonAsync($"/api/patients/{patient.Id}", new UpdatePatientRequest
            {
                FirstName = patient.FirstName, LastName = patient.LastName, DateOfBirth = patient.DateOfBirth, Email = patient.Email,
                Postcode = "ZZ9 9ZZ", MobilePhone = ""
            }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        var updates = await (await _admin.GetAsync($"/api/activity/search?PageSize=10&Actions=PatientUpdated&PatientId={patient.Id}"))
            .ReadAsAsync<PagedResponse<ActivityEntryResponse>>();
        var details = updates.Items.Single().Details;
        details.Should().Contain("postcode").And.Contain("mobilePhone");
        details.Should().NotContain("ZZ9").And.NotContain("7700");

        // The practice can't be deleted while a patient has it, and the database refuses too.
        await (await (await ClinicalAsync(_admin)).DeleteAsync($"{Practices}/{practice.Id}")).ShouldHaveStatusAsync(HttpStatusCode.Conflict);

        var fhir = await (await _admin.GetAsync($"/api/fhir/Patient/{patient.Id}")).Content.ReadFromJsonAsync<FhirPatient>();
        fhir!.Address!.Single().PostalCode.Should().Be("ZZ9 9ZZ");
        fhir.GeneralPractitioner!.Single().Identifier!.Value.Should().Be(practice.OdsCode);
        fhir.Telecom!.Should().NotContain(t => t.System == "phone");

        var export = await (await _admin.GetAsync($"/api/patients/{patient.Id}/export")).ReadAsAsync<PatientExportResponse>();
        export.Patient.Should().BeEquivalentTo(new { Postcode = "ZZ9 9ZZ", PracticeName = practice.Name, PracticeOdsCode = practice.OdsCode });
        export.Patient.AddressLines.Should().Equal(AddressLine, Town);

        // Anonymising removes the contact details and the link; the practice stays in the directory.
        await (await _admin.PostAsJsonAsync($"/api/patients/{patient.Id}/anonymise", new AnonymisePatientRequest
            {
                Reason = "RetentionPeriodEnded", Confirmation = $"{patient.FirstName} {patient.LastName}"
            }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        var anonymised = await (await _admin.GetAsync($"/api/patients/{patient.Id}")).ReadAsAsync<PatientResponse>();
        anonymised.Should().BeEquivalentTo(new { Postcode = (string?)null, MobilePhone = (string?)null, PracticeId = (Guid?)null });
        anonymised.AddressLines.Should().BeEmpty();
        (await GetPracticeAsync(practice.Id)).Should().BeEquivalentTo(new { PatientCount = 0, CanBeDeleted = true });
    }

    [Test]
    public async Task Patient_ArchivedPracticeIsKeptButNotNewlyChosen_AndBadDetailsAre400s()
    {
        var practice = await CreatePracticeAsync(_admin);
        var patient = await CreatePatientWithContactDetailsAsync(practice.Id);
        await (await (await ClinicalAsync(_admin)).PostAsJsonAsync($"{Practices}/{practice.Id}/archive", new PracticeVersionRequest { ExpectedVersion = 1 }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        var edit = new UpdatePatientRequest
        {
            FirstName = patient.FirstName, LastName = patient.LastName, DateOfBirth = patient.DateOfBirth, Email = patient.Email,
            PracticeId = practice.Id
        };
        (await (await (await ClinicalAsync(_admin)).PatchAsJsonAsync($"/api/patients/{patient.Id}", edit)).ReadAsAsync<PatientResponse>())
            .Practice!.IsArchived.Should().BeTrue();

        var other = await CreatePatientAsync(_admin);
        await (await (await ClinicalAsync(_admin)).PatchAsJsonAsync($"/api/patients/{other.Id}", new UpdatePatientRequest
            {
                FirstName = other.FirstName, LastName = other.LastName, DateOfBirth = other.DateOfBirth, Email = other.Email,
                PracticeId = practice.Id
            }))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);

        await (await (await ClinicalAsync(_admin)).PostAsJsonAsync("/api/patients", new CreatePatientRequest
            {
                FirstName = "Synthetic", LastName = "Person", DateOfBirth = DateOfBirth, Email = $"x@{PatientEmailDomain}",
                Postcode = "nowhere", MobilePhone = "01632 960000", AddressLines = ["a", "b", "c", "d"]
            }))
            .ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Referral_PracticeFromTheDirectory_FillsWhatsMissing_AndKeepsThePracticeInUse()
    {
        var practice = await CreatePracticeAsync(_admin, UniqueOdsCode());
        var patient = await CreatePatientAsync(_admin);

        var referral = await (await (await ClinicalAsync(_admin)).PostAsJsonAsync("/api/referrals", new RegisterReferralRequest
            {
                PatientId = patient.Id,
                TeamId = Team.GeneralId,
                ReceivedOn = UkToday.AddDays(-2),
                PracticeId = practice.Id,
                ReferrerPractice = "",
                RequestedPriority = "Medium",
                Reason = "Synthetic reason for referral."
            }))
            .ReadAsAsync<ReferralResponse>(HttpStatusCode.Created);

        referral.Should().BeEquivalentTo(new { PracticeId = practice.Id, ReferrerPractice = practice.Name, ReferrerOdsCode = practice.OdsCode });
        (await GetPracticeAsync(practice.Id)).Should().BeEquivalentTo(new { ReferralCount = 1, CanBeDeleted = false });

        var edited = await (await (await ClinicalAsync(_admin)).PutAsJsonAsync($"/api/referrals/{referral.Id}", new EditReferralRequest
            {
                TeamId = Team.GeneralId,
                ReceivedOn = referral.ReceivedOn,
                ReferrerPractice = referral.ReferrerPractice,
                ReferrerOdsCode = referral.ReferrerOdsCode,
                RequestedPriority = "Medium",
                Reason = referral.Reason,
                ExpectedVersion = referral.Version
            }))
            .ReadAsAsync<ReferralResponse>();
        edited.PracticeId.Should().BeNull();
        edited.History.Last().ChangedFields.Should().Equal("directoryPractice");
        (await GetPracticeAsync(practice.Id)).CanBeDeleted.Should().BeTrue();
    }

    [Test]
    public async Task StaffDetails_SetByThemselvesOrAnAdmin_LoggedByFieldName_AndOnTheUsersList()
    {
        var staff = await CreateStaffAsync("Nurse");

        var mine = await (await staff.Client.PutAsJsonAsync("/api/auth/me/details", new StaffDetailsRequest
            {
                FullName = "  Sam   Synthetic ", JobTitle = "Synthetic nurse"
            }))
            .ReadAsAsync<StaffDetailsResponse>();
        mine.Should().BeEquivalentTo(new { FullName = "Sam Synthetic", JobTitle = "Synthetic nurse" });
        (await (await staff.Client.GetAsync("/api/auth/me/details")).ReadAsAsync<StaffDetailsResponse>()).FullName
            .Should().Be("Sam Synthetic");

        // Only holders of ManageUsers set someone else's.
        await (await staff.Client.PutAsJsonAsync($"/api/auth/users/{AdminUsername}/details", new StaffDetailsRequest { FullName = "Nope" }))
            .ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await (await _admin.PutAsJsonAsync($"/api/auth/users/{staff.Username}/details", new StaffDetailsRequest
            {
                FullName = "Sam Synthetic", JobTitle = "Synthetic senior nurse"
            }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await _admin.PutAsJsonAsync("/api/auth/users/nobody-at-all/details", new StaffDetailsRequest()))
            .ShouldHaveStatusAsync(HttpStatusCode.NotFound);
        await (await staff.Client.PutAsJsonAsync("/api/auth/me/details", new StaffDetailsRequest { FullName = new string('a', 101) }))
            .ShouldHaveStatusAsync(HttpStatusCode.BadRequest);

        var user = await (await _admin.GetAsync($"/api/auth/users/{staff.Username}")).ReadAsAsync<UserResponse>();
        user.Should().BeEquivalentTo(new { FullName = "Sam Synthetic", JobTitle = "Synthetic senior nurse" });

        var entries = await (await _admin.GetAsync("/api/activity/search?PageSize=100&Actions=StaffDetailsChanged"))
            .ReadAsAsync<PagedResponse<ActivityEntryResponse>>();
        entries.Items.Should().HaveCount(2).And.OnlyContain(e => e.TargetUsername == staff.Username && !e.Details.Contains("Synthetic"));
        entries.Items.Should().Contain(e => e.Username == staff.Username && e.Details.Contains("fullName") && e.Details.Contains("jobTitle"));
        entries.Items.Should().Contain(e => e.Username == AdminUsername && e.Details == """{"fields":["jobTitle"]}""");

        // Their session survives: details aren't how anyone signs in.
        await (await staff.Client.GetAsync("/api/auth/me/details")).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    private async Task<PracticeResponse> GetPracticeAsync(Guid id) =>
        await (await _admin.GetAsync($"{Practices}/{id}")).ReadAsAsync<PracticeResponse>();
}
