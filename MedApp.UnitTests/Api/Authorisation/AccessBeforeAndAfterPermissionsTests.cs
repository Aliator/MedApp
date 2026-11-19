using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using MedApp.API.Common.Extensions;
using MedApp.API.Controllers;
using MedApp.API.Notifications;
using MedApp.Application.Common.Identity;
using MedApp.UnitTests.Common.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.UnitTests.Api.Authorisation;

/// <summary>
/// The proof that roles and permissions changed nothing for existing accounts: endpoint by endpoint, who could call it
/// before (frozen below, from the code just before permissions, when policies checked the roles Admin and User), and who
/// can now, with the permissions the migration gives Admin and "All staff (before roles)" (the old User role).
/// <para>
/// Columns: nobody signed in; signed in with no role; signed in with only a role an admin made (no permissions but
/// UseApp); User, now "All staff (before roles)"; Admin. 1 is allowed.
/// </para>
/// <para>
/// A new endpoint fails here until it's added to the table, with who may call it: decide that on purpose.
/// </para>
/// </summary>
[TestFixture]
public sealed class AccessBeforeAndAfterPermissionsTests
{
    /// <summary>
    /// Who could call each endpoint before permissions (the Staff policy and Roles = "Admin"); for an endpoint added
    /// since, who may call it with the permissions the standard roles have. The specimen steps need RecordSpecimens or
    /// ProcessSpecimens, which neither Admin nor "All staff (before roles)" has (they're clinical: decision 74), and so
    /// do seeing, signing off and reassigning results (SignOffResults), and recording, withdrawing and
    /// scanning consent (RecordConsent). Every data-request endpoint, its review included, needs ManagePrivacy (only
    /// admins deal with data requests: the owner's decision).
    /// </summary>
    private const string Before = """
        AccountSecurityController.BeginAuthenticatorSetup 00011
        AccountSecurityController.BeginEmailCodesSetup 00011
        AccountSecurityController.ChangeMyEmail 00011
        AccountSecurityController.ConfirmEmail 01111
        AccountSecurityController.DisableTwoFactor 00011
        AccountSecurityController.EnableEmailCodes 00011
        AccountSecurityController.EnableTwoFactor 00011
        AccountSecurityController.ForgotPassword 11111
        AccountSecurityController.GetFeatures 11111
        AccountSecurityController.GetMyEmail 00011
        AccountSecurityController.GetMyTwoFactor 00011
        AccountSecurityController.GetSecuritySettings 00001
        AccountSecurityController.LoginTwoFactor 11111
        AccountSecurityController.RegenerateRecoveryCodes 00011
        AccountSecurityController.RemoveMyEmail 00011
        AccountSecurityController.RemoveUserEmail 00001
        AccountSecurityController.ResendSignInCode 11111
        AccountSecurityController.ResetPassword 11111
        AccountSecurityController.ResetUserTwoFactor 00001
        AccountSecurityController.SaveSecuritySettings 00001
        AccountSecurityController.SendTwoFactorEmailCode 00011
        ActivityController.SearchActivity 00001
        AppointmentsController.BookAppointment 00011
        AppointmentsController.CancelAppointment 00011
        AppointmentsController.CreateAppointment 00011
        AppointmentsController.GetAppointmentById 00011
        AppointmentsController.GetAppointmentMessages 00011
        AppointmentsController.GetPatientMessagingStatus 00011
        AppointmentsController.MarkEnteredInError 00011
        AppointmentsController.RecordDnaFollowUp 00011
        AppointmentsController.RecordOutcome 00011
        AppointmentsController.RecordPastAppointment 00011
        AppointmentsController.SearchAppointments 00011
        AppointmentsController.UpdateNotes 00011
        AttachmentsController.Delete 00011
        AttachmentsController.Download 00011
        AttachmentsController.GetAttachments 00011
        AttachmentsController.SetClinicalPhoto 00011
        AttachmentsController.Thumbnail 00011
        AttachmentsController.Upload 00011
        AuthController.CreateUser 00001
        AuthController.DeleteUser 00001
        AuthController.ExtendSession 01111
        AuthController.GetAllRoles 00001
        AuthController.GetAllUsers 00001
        AuthController.GetMyDetails 00011
        AuthController.GetMySettings 00011
        AuthController.GetMyTeams 00011
        AuthController.GetSession 01111
        AuthController.GetUserByUsername 00001
        AuthController.GetUserRoles 00001
        AuthController.GetUserTeams 00001
        AuthController.Login 11111
        AuthController.Logout 11111
        AuthController.ResetUserPassword 00001
        AuthController.SaveMySettings 00011
        AuthController.SearchUsers 00001
        AuthController.SetMyDetails 00011
        AuthController.SetUserDetails 00001
        AuthController.SetUserRoles 00001
        AuthController.SetUserTeams 00001
        AuthController.SignOutEverywhere 00001
        AuthController.UpdateUserPassword 00011
        AuthController.WhoAmI 11111
        CalendarController.ApplyDayTemplate 00011
        CalendarController.ChangeTemplateBlockForDay 00011
        CalendarController.ClearDayTemplate 00011
        CalendarController.CreateBlock 00011
        CalendarController.DeleteBlock 00011
        CalendarController.GetAvailability 00001
        CalendarController.GetCalendar 00011
        CalendarController.GetStaff 00011
        CalendarController.GetStaffHours 00011
        CalendarController.GetTeamCalendar 00011
        CalendarController.GetWeekTemplate 00011
        CalendarController.RemoveTemplateBlockForDay 00011
        CalendarController.SetStaffHours 00011
        CalendarController.SetWeekTemplate 00011
        CalendarController.UpdateBlock 00011
        CalendarSetupController.AddBankHolidays 00001
        CalendarSetupController.ArchiveActivityType 00001
        CalendarSetupController.ArchiveDayTemplate 00011
        CalendarSetupController.ArchiveWeekTemplate 00011
        CalendarSetupController.CreateActivityType 00001
        CalendarSetupController.CreateClosedDay 00001
        CalendarSetupController.CreateDayTemplate 00011
        CalendarSetupController.CreateWeekTemplate 00011
        CalendarSetupController.DeleteActivityType 00001
        CalendarSetupController.DeleteClosedDay 00001
        CalendarSetupController.DeleteDayTemplate 00011
        CalendarSetupController.DeleteWeekTemplate 00011
        CalendarSetupController.GetActivityType 00011
        CalendarSetupController.GetActivityTypes 00011
        CalendarSetupController.GetClosedDayBookings 00001
        CalendarSetupController.GetClosedDays 00011
        CalendarSetupController.GetDayTemplate 00011
        CalendarSetupController.GetDayTemplates 00011
        CalendarSetupController.GetWeekTemplate 00011
        CalendarSetupController.GetWeekTemplates 00011
        CalendarSetupController.RestoreActivityType 00001
        CalendarSetupController.RestoreDayTemplate 00011
        CalendarSetupController.RestoreWeekTemplate 00011
        CalendarSetupController.UpdateActivityType 00001
        CalendarSetupController.UpdateDayTemplate 00011
        CalendarSetupController.UpdateWeekTemplate 00011
        ClinicsController.CancelSession 00001
        ClinicsController.CreatePattern 00001
        ClinicsController.CreateSession 00001
        ClinicsController.CreateSessions 00001
        ClinicsController.GetPatterns 00001
        ClinicsController.GetSession 00011
        ClinicsController.SearchSessions 00011
        ClinicsController.UpdatePattern 00001
        ClinicsController.UpdateSession 00001
        ConsentController.ArchiveConsentForm 00001
        ConsentController.AttachConsentScan 00000
        ConsentController.CreateConsentForm 00001
        ConsentController.DeleteConsentForm 00001
        ConsentController.GetConsentForms 00011
        ConsentController.GetPathwayConsentRecords 00011
        ConsentController.MarkConsentEnteredInError 00011
        ConsentController.RecordConsent 00000
        ConsentController.RestoreConsentForm 00001
        ConsentController.UpdateConsentForm 00001
        ConsentController.WithdrawConsent 00000
        ConsultsController.Close 00011
        ConsultsController.GetForPathway 00011
        ConsultsController.GetIncoming 00011
        ConsultsController.Reply 00011
        ConsultsController.Send 00011
        ConsultsController.Withdraw 00011
        DataRequestsController.CreateDataRequest 00001
        DataRequestsController.DownloadDataRequestPack 00001
        DataRequestsController.ExtendDataRequest 00001
        DataRequestsController.GetDataRequest 00001
        DataRequestsController.GetDataRequestReviewItems 00001
        DataRequestsController.RecordDataRequestReview 00001
        DataRequestsController.RecordDataRequestSent 00001
        DataRequestsController.RecordIdentityCheck 00001
        DataRequestsController.RefuseDataRequest 00001
        DataRequestsController.SearchDataRequests 00001
        DataRequestsController.WithdrawDataRequest 00001
        DemographicsController.GetStatus 00011
        DemographicsController.LookUp 00011
        FhirController.Metadata 11111
        FhirController.ReadPatient 00011
        FhirController.SearchPatients 00011
        HospitalRequestsController.Get 00011
        HospitalRequestsController.PatientMatches 00000
        HospitalRequestsController.Register 00000
        HospitalRequestsController.Search 00011
        HospitalsController.Archive 00001
        HospitalsController.Create 00001
        HospitalsController.Delete 00001
        HospitalsController.GetAll 00011
        HospitalsController.Restore 00001
        HospitalsController.Update 00001
        JobsController.GetJobs 00001
        JobsController.RunJob 00001
        LabCasesController.AddSurgeryDetails 00000
        LabCasesController.Authorise 00000
        LabCasesController.Get 00011
        LabCasesController.MarkEnteredInError 00011
        LabCasesController.MarkStepEnteredInError 00000
        LabCasesController.Open 00000
        LabCasesController.RecordOutcome 00000
        LabCasesController.RecordStep 00000
        LabCasesController.Search 00011
        LetterTemplatesController.ArchiveLetterTemplate 00001
        LetterTemplatesController.CreateLetterTemplate 00001
        LetterTemplatesController.DeleteLetterTemplate 00001
        LetterTemplatesController.GetLetterTemplate 00011
        LetterTemplatesController.GetLetterTemplates 00011
        LetterTemplatesController.GetMergeFields 00011
        LetterTemplatesController.PreviewLetterTemplate 00001
        LetterTemplatesController.RestoreLetterTemplate 00001
        LetterTemplatesController.UpdateLetterTemplate 00001
        LettersController.ApproveLetter 00000
        LettersController.CountLetters 00011
        LettersController.CreateLetter 00000
        LettersController.DiscardLetter 00000
        LettersController.GetLetter 00011
        LettersController.PreviewLetter 00011
        LettersController.RecordSent 00000
        LettersController.ReturnLetter 00000
        LettersController.SearchLetters 00011
        LettersController.SendLetter 00000
        LettersController.SubmitLetter 00000
        LettersController.UpdateLetter 00000
        LettersController.WithdrawLetter 00011
        LettersController.WriteCorrection 00000
        MdtController.ChoosePresenter 00011
        MdtController.Defer 00011
        MdtController.GetAwaitingMeeting 00011
        MdtController.GetMeetingList 00011
        MdtController.GetMeetings 00011
        MdtController.GetPathwayItems 00011
        MdtController.ListForMdt 00011
        MdtController.RecordOutcome 00000
        MdtController.Withdraw 00011
        MeetingsController.CancelMeeting 00001
        MeetingsController.CreateMeeting 00001
        MeetingsController.GetMeeting 00011
        MeetingsController.ReplyToMeeting 00011
        MeetingsController.UpdateMeeting 00001
        MonitoringController.Create 00001
        MonitoringController.Delete 00001
        MonitoringController.GetMine 00001
        MonitoringController.SetEnabled 00001
        MonitoringController.Update 00001
        NoteDeletionRequestsController.DecideNoteDeletionRequest 00001
        NoteDeletionRequestsController.GetNoteDeletionRequest 00001
        NotepadController.GetNotepad 01111
        NotepadController.SaveNotepad 01111
        NotificationsController.GetMine 00011
        NotificationsController.GetMyPreferences 00011
        NotificationsController.MarkAllRead 00011
        NotificationsController.MarkRead 00011
        NotificationsController.UpdateMyPreferences 00011
        NotificationsHub.Hub 00011
        PathwayAuditController.SearchPathwayAudit 00001
        PathwayInsightsController.GetPathwayInsights 00001
        PatientPathwayStagesController.ArchivePathwayTemplate 00001
        PatientPathwayStagesController.ArchivePatientPathwayStageDefinition 00001
        PatientPathwayStagesController.CreatePathwayTemplate 00001
        PatientPathwayStagesController.CreatePatientPathwayStageDefinition 00001
        PatientPathwayStagesController.DeletePathwayTemplate 00001
        PatientPathwayStagesController.DeletePatientPathwayStageDefinition 00001
        PatientPathwayStagesController.GetAllPathwayTemplates 00011
        PatientPathwayStagesController.GetAllPatientPathwayStageDefinitions 00011
        PatientPathwayStagesController.GetPathwayTemplateById 00011
        PatientPathwayStagesController.GetPatientPathwayStageDefinitionById 00011
        PatientPathwayStagesController.RestorePathwayTemplate 00001
        PatientPathwayStagesController.RestorePatientPathwayStageDefinition 00001
        PatientPathwayStagesController.UpdatePathwayTemplate 00001
        PatientPathwayStagesController.UpdatePatientPathwayStageDefinition 00001
        PatientPathwaysController.AddPathwayBranch 00011
        PatientPathwaysController.AddPathwayNote 00011
        PatientPathwaysController.AssignPatientPathway 00001
        PatientPathwaysController.ChangePatientPathwayStatus 00011
        PatientPathwaysController.CreatePatientPathway 00011
        PatientPathwaysController.DeletePathwayNote 00001
        PatientPathwaysController.DeletePatientPathway 00001
        PatientPathwaysController.EditPathwayNote 00011
        PatientPathwaysController.FinishPathwayStage 00011
        PatientPathwaysController.GetPatientPathwayById 00011
        PatientPathwaysController.GetPatientPathwayLabels 00011
        PatientPathwaysController.PickUpPathwayStage 00011
        PatientPathwaysController.ReinstatePathwayStage 00001
        PatientPathwaysController.RequestNoteDeletion 00011
        PatientPathwaysController.SearchPatientPathways 00011
        PatientPathwaysController.SkipPathwayStage 00011
        PatientPathwaysController.StartPathwayStage 00011
        PatientPathwaysController.UnassignPatientPathway 00001
        PatientPathwaysController.UnlockPathwayStage 00001
        PatientPathwaysController.UpdatePathwayStageNotes 00011
        PatientPathwaysController.UpdatePatientPathway 00011
        PatientTimelineController.GetOverview 00011
        PatientTimelineController.GetTimeline 00011
        PatientsController.AnonymisePatient 00001
        PatientsController.CreatePatient 00011
        PatientsController.DeletePatient 00011
        PatientsController.ExportPatient 00001
        PatientsController.GetAllPatients 00011
        PatientsController.GetPatientById 00011
        PatientsController.GetPatientLabels 00011
        PatientsController.KeepPatientRecord 00001
        PatientsController.SearchPatients 00011
        PatientsController.SearchRetentionReview 00001
        PatientsController.SetDispute 00001
        PatientsController.SetRestriction 00001
        PatientsController.UpdatePatient 00011
        PracticesController.ArchivePractice 00011
        PracticesController.CreatePractice 00011
        PracticesController.DeletePractice 00011
        PracticesController.GetPractice 00011
        PracticesController.GetPractices 00011
        PracticesController.RestorePractice 00011
        PracticesController.UpdatePractice 00011
        ReferralsController.AcceptReferral 00000
        ReferralsController.AskForInformation 00000
        ReferralsController.CountAwaitingTriage 00011
        ReferralsController.EditReferral 00011
        ReferralsController.GetReferral 00011
        ReferralsController.InformationReceived 00000
        ReferralsController.MarkEnteredInError 00011
        ReferralsController.PauseClock 00011
        ReferralsController.RedirectReferral 00000
        ReferralsController.RegisterReferral 00011
        ReferralsController.RejectReferral 00000
        ReferralsController.ResumeClock 00011
        ReferralsController.SearchReferrals 00011
        ReferralsController.WithdrawReferral 00011
        ResultsController.CountResults 00011
        ResultsController.GetResult 00011
        ResultsController.MarkEnteredInError 00011
        ResultsController.MarkSeen 00000
        ResultsController.Reassign 00000
        ResultsController.RecordExternalResult 00011
        ResultsController.SearchResults 00011
        ResultsController.SignOff 00000
        RolesController.ArchiveRole 00001
        RolesController.CreateRole 00001
        RolesController.DeleteRole 00001
        RolesController.GetPermissions 00001
        RolesController.GetRole 00001
        RolesController.GetRoles 00001
        RolesController.RestoreRole 00001
        RolesController.UpdateRole 00001
        SitesController.ArchiveRoom 00001
        SitesController.ArchiveSite 00001
        SitesController.CreateRoom 00001
        SitesController.CreateSite 00001
        SitesController.DeleteRoom 00001
        SitesController.DeleteSite 00001
        SitesController.GetRooms 00011
        SitesController.GetSite 00011
        SitesController.GetSiteHours 00011
        SitesController.GetSites 00011
        SitesController.RestoreRoom 00001
        SitesController.RestoreSite 00001
        SitesController.SetSiteHours 00001
        SitesController.UpdateRoom 00001
        SitesController.UpdateSite 00001
        SpecimenSetupController.ArchiveLabTest 00001
        SpecimenSetupController.ArchiveType 00001
        SpecimenSetupController.CreateLabTest 00001
        SpecimenSetupController.CreateType 00001
        SpecimenSetupController.DeleteLabTest 00001
        SpecimenSetupController.DeleteType 00001
        SpecimenSetupController.GetLabTests 00011
        SpecimenSetupController.GetTypes 00011
        SpecimenSetupController.RestoreLabTest 00001
        SpecimenSetupController.RestoreType 00001
        SpecimenSetupController.UpdateLabTest 00001
        SpecimenSetupController.UpdateType 00001
        SpecimensController.EditSpecimen 00000
        SpecimensController.FindSpecimen 00011
        SpecimensController.GetSpecimen 00011
        SpecimensController.MarkEnteredInError 00011
        SpecimensController.MarkLost 00011
        SpecimensController.MarkReceived 00000
        SpecimensController.MarkSent 00000
        SpecimensController.RecordSpecimen 00000
        SpecimensController.Reject 00000
        SpecimensController.Report 00000
        SpecimensController.SearchSpecimenWorklist 00011
        SpecimensController.SearchSpecimens 00011
        TeamsController.ArchiveTeam 00001
        TeamsController.CreateTeam 00001
        TeamsController.DeleteTeam 00001
        TeamsController.GetTeam 00011
        TeamsController.GetTeams 00011
        TeamsController.RestoreTeam 00001
        TeamsController.UpdateTeam 00001
        """;

    private static readonly (string Name, ClaimsPrincipal? User)[] Callers =
    [
        ("nobody signed in", null),
        ("no role", Caller()),
        ("only a role an admin made", Caller("Visitor")),
        ("All staff (before roles), was User", Caller(AppRoles.AllStaffBeforeRoles)),
        ("Admin", Caller(AppRoles.Admin)),
    ];

    /// <summary>The column of an account that held only a role an admin made through the old role endpoints.</summary>
    private const int CustomRoleColumn = 2;

    /// <summary>The Admin column.</summary>
    private const int AdminColumn = 4;

    /// <summary>Signed in with these roles, and the permissions they grant now, as the session handler builds them.</summary>
    private static ClaimsPrincipal Caller(params string[] roles) => ApiTestConstants.SignedIn(roles);

    private static IEnumerable<(string Name, IReadOnlyList<AuthorizeAttribute> Attributes, bool Anonymous)> Endpoints()
    {
        foreach (var type in typeof(AuthController).Assembly.GetTypes()
                     .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
                     .OrderBy(t => t.Name))
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).OrderBy(m => m.Name))
            yield return (
                $"{type.Name}.{method.Name}",
                type.GetCustomAttributes<AuthorizeAttribute>().Concat(method.GetCustomAttributes<AuthorizeAttribute>()).ToList(),
                method.GetCustomAttributes<AllowAnonymousAttribute>().Any());

        yield return ("NotificationsHub.Hub", typeof(NotificationsHub).GetCustomAttributes<AuthorizeAttribute>().ToList(), false);
    }

    private static async Task<Dictionary<string, string>> AccessNowAsync()
    {
        await using var services = new ServiceCollection().AddLogging().AddApi(new ConfigurationBuilder().Build()).BuildServiceProvider();
        var provider = services.GetRequiredService<IAuthorizationPolicyProvider>();
        var authorization = services.GetRequiredService<IAuthorizationService>();

        var access = new Dictionary<string, string>();
        foreach (var (name, attributes, anonymous) in Endpoints())
        {
            var cells = new List<char>();
            foreach (var (_, user) in Callers)
            {
                bool allowed;
                if (anonymous || attributes.Count == 0)
                    allowed = true;
                else if (user is null)
                    allowed = false;
                else
                {
                    var policy = await AuthorizationPolicy.CombineAsync(provider, attributes);
                    // A {username} route naming the caller, for SelfUserOnly (their own password).
                    var http = new DefaultHttpContext();
                    http.Request.RouteValues["username"] = user.Identity!.Name;
                    allowed = (await authorization.AuthorizeAsync(user, http, policy!)).Succeeded;
                }

                cells.Add(allowed ? '1' : '0');
            }

            access[name] = new string([.. cells]);
        }

        return access;
    }

    private static Dictionary<string, string> AccessBefore() =>
        Before.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(' '))
            .ToDictionary(parts => parts[0], parts => parts[1]);

    [Test]
    public async Task EveryEndpoint_IsInTheTable_AndEveryRowIsAnEndpoint()
    {
        var now = await AccessNowAsync();

        now.Keys.Except(AccessBefore().Keys).Should().BeEmpty("a new endpoint needs a row saying who may call it");
        AccessBefore().Keys.Except(now.Keys).Should().BeEmpty("an endpoint that's gone needs its row removed");
    }

    [Test]
    public async Task Admin_AllStaff_NoRoleAndNobody_CanCallExactlyWhatTheyCouldBefore()
    {
        var now = await AccessNowAsync();
        var before = AccessBefore();

        // Admins became read only on purpose (decisions 182 and 208 of docs/multi-clinic-plan.md): they lose every
        // endpoint that needs a clinical permission (Permissions.NotForAdmin), and nothing else.
        var readOnlyAdmin = Endpoints()
            .Where(e => e.Attributes.OfType<MedApp.API.Common.Authentication.RequirePermissionAttribute>()
                .Any(a => Permissions.NotForAdmin.Contains(a.Permission)))
            .Select(e => e.Name)
            .ToHashSet();
        var differences = before.Keys.Intersect(now.Keys)
            .SelectMany(endpoint => Enumerable.Range(0, Callers.Length)
                .Where(column => column != CustomRoleColumn && before[endpoint][column] != now[endpoint][column])
                .Where(column => !(column == AdminColumn && now[endpoint][column] == '0' && readOnlyAdmin.Contains(endpoint)))
                .Select(column => $"{endpoint}: {Callers[column].Name} {(now[endpoint][column] == '1' ? "gained" : "lost")} it"))
            .ToList();

        differences.Should().BeEmpty();
    }

    /// <summary>
    /// The one deliberate difference (decision 54): every role grants UseApp, so an account holding only a role an admin
    /// made through the old role endpoints can now use what UseApp covers (its own account, notifications and calendar,
    /// reading calendars and the setup lists). It gains nothing else, and loses nothing.
    /// </summary>
    [Test]
    public async Task OnlyARoleAnAdminMade_GainsJustWhatUseAppAllows()
    {
        var now = await AccessNowAsync();
        var before = AccessBefore();
        var useAppOnly = Endpoints()
            .Where(e => e.Attributes.OfType<MedApp.API.Common.Authentication.RequirePermissionAttribute>().All(a => a.Permission == Permission.UseApp))
            .Select(e => e.Name)
            .ToHashSet();

        foreach (var endpoint in before.Keys.Intersect(now.Keys))
        {
            var was = before[endpoint][CustomRoleColumn];
            var isNow = now[endpoint][CustomRoleColumn];
            if (was == '1')
                isNow.Should().Be('1', $"{endpoint} mustn't be lost");
            else if (isNow == '1')
                useAppOnly.Should().Contain(endpoint, $"{endpoint} is gained only through UseApp");
        }
    }

    /// <summary>
    /// The deliberate loss of decision 138 (<c>docs/centre-phases-3-5-plan.md</c>, PR 24): <c>OverrideStages</c> split
    /// into <see cref="Permission.UnlockStages"/> and <see cref="Permission.FinishWithoutVisit"/>. The built-in Admin role
    /// keeps UnlockStages (unlocking and reinstating, whose rows above are unchanged) and loses FinishWithoutVisit, which
    /// handlers check (finishing, or skipping, a stage whose visit is Required without it), and so its notices too. That's
    /// all Admin loses. The endpoints can't show it, as finishing and skipping need only WorkPathways, so this checks the
    /// permissions each session is given. All staff (before roles) had neither half, and gains neither; the starting
    /// Clinician role, which had OverrideStages, keeps both (the migration renames its rows).
    /// </summary>
    [Test]
    public void SplitOverrideStages_AdminLosesOnlyFinishingWithoutAVisit_AndNobodyGainsEither()
    {
        // Admin had every permission but triage, OverrideStages among them (here, as its two halves).
        // The clinical permissions that came later (specimens, letters, MDT outcomes, consent) were never Admin's.
        var cameLater = Permissions.NotForAdmin.Except([Permission.TriageReferrals, Permission.FinishWithoutVisit]).ToHashSet();
        var adminBefore = Enum.GetValues<Permission>()
            .Where(p => p != Permission.TriageReferrals && !cameLater.Contains(p))
            .ToHashSet();
        var adminNow = TestPermissions.ForRoles(AppRoles.Admin);

        adminBefore.Except(adminNow).Should().Equal(Permission.FinishWithoutVisit);
        adminNow.Except(adminBefore).Should().BeEmpty();
        TestPermissions.ForRoles(AppRoles.AllStaffBeforeRoles).Should()
            .NotContain(Permission.UnlockStages).And.NotContain(Permission.FinishWithoutVisit);
        TestPermissions.ForRoles(StandardRoles.Clinician.Name).Should()
            .Contain(Permission.UnlockStages).And.Contain(Permission.FinishWithoutVisit);
    }
}
