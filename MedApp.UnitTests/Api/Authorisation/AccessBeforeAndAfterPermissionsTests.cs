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
    /// ProcessSpecimens, which neither Admin nor "All staff (before roles)" has (they're clinical: decision 74).
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
        AppointmentsController.MarkEnteredInError 00011
        AppointmentsController.RecordOutcome 00011
        AppointmentsController.RecordPastAppointment 00011
        AppointmentsController.SearchAppointments 00011
        AppointmentsController.UpdateNotes 00011
        AttachmentsController.Delete 00011
        AttachmentsController.Download 00011
        AttachmentsController.GetAttachments 00011
        AttachmentsController.Thumbnail 00011
        AttachmentsController.Upload 00011
        AuthController.AssignRole 00001
        AuthController.CreateRole 00001
        AuthController.CreateUser 00001
        AuthController.DeleteRole 00001
        AuthController.DeleteUser 00001
        AuthController.ExtendSession 01111
        AuthController.GetAllRoles 00001
        AuthController.GetAllUsers 00001
        AuthController.GetMySettings 00011
        AuthController.GetMySpecialties 00011
        AuthController.GetSession 01111
        AuthController.GetUserByUsername 00001
        AuthController.GetUserRoles 00001
        AuthController.GetUserSpecialties 00001
        AuthController.Login 11111
        AuthController.Logout 11111
        AuthController.ResetUserPassword 00001
        AuthController.RevokeRole 00001
        AuthController.SaveMySettings 00011
        AuthController.SearchUsers 00001
        AuthController.SetUserRoles 00001
        AuthController.SetUserSpecialties 00001
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
        CalendarController.GetTeamCalendar 00001
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
        DemographicsController.GetStatus 00011
        DemographicsController.LookUp 00011
        FhirController.Metadata 11111
        FhirController.ReadPatient 00011
        FhirController.SearchPatients 00011
        JobsController.GetJobs 00001
        JobsController.RunJob 00001
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
        PatientPathwaysController.RequestNoteDeletion 00011
        PatientPathwaysController.SearchPatientPathways 00011
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
        ReferralsController.CountAwaitingTriage 00011
        ReferralsController.EditReferral 00011
        ReferralsController.GetReferral 00011
        ReferralsController.MarkEnteredInError 00011
        ReferralsController.RegisterReferral 00011
        ReferralsController.SearchReferrals 00011
        ReferralsController.WithdrawReferral 00011
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
        SpecimenSetupController.ArchiveLaboratory 00001
        SpecimenSetupController.ArchiveType 00001
        SpecimenSetupController.CreateLaboratory 00001
        SpecimenSetupController.CreateType 00001
        SpecimenSetupController.DeleteLaboratory 00001
        SpecimenSetupController.DeleteType 00001
        SpecimenSetupController.GetLaboratories 00011
        SpecimenSetupController.GetTypes 00011
        SpecimenSetupController.RestoreLaboratory 00001
        SpecimenSetupController.RestoreType 00001
        SpecimenSetupController.UpdateLaboratory 00001
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
        SpecimensController.SearchSpecimens 00011
        SpecialtiesController.ArchiveSpecialty 00001
        SpecialtiesController.CreateSpecialty 00001
        SpecialtiesController.DeleteSpecialty 00001
        SpecialtiesController.GetSpecialties 00011
        SpecialtiesController.GetSpecialty 00011
        SpecialtiesController.RestoreSpecialty 00001
        SpecialtiesController.UpdateSpecialty 00001
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

        var differences = before.Keys.Intersect(now.Keys)
            .SelectMany(endpoint => Enumerable.Range(0, Callers.Length)
                .Where(column => column != CustomRoleColumn && before[endpoint][column] != now[endpoint][column])
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
}
