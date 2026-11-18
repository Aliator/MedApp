using MediatR;
using MedApp.Application.Activity.Common;
using MedApp.Application.Authentication.Emails.Commands.ConfirmEmail;
using MedApp.Application.Authentication.Roles.Commands.AssignRole;
using MedApp.Application.Authentication.Roles.Commands.CreateRole;
using MedApp.Application.Authentication.Roles.Commands.SetRoleArchived;
using MedApp.Application.Authentication.Roles.Commands.SetUserRoles;
using MedApp.Application.Authentication.Roles.Repositories;
using MedApp.Application.Authentication.TwoFactor.Commands.BeginAuthenticatorSetup;
using MedApp.Application.Authentication.TwoFactor.Commands.EnableTwoFactor;
using MedApp.Application.Authentication.Users.Commands.CreateUser;
using MedApp.Application.Authentication.Users.Commands.ResetUserPassword;
using MedApp.Application.Authentication.Users.Commands.UpdateUserPassword;
using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Commands.UpdateMyNotificationPreferences;
using MedApp.Contracts.Roles.Responses;
using MedApp.Application.Pathways.Common;
using MedApp.DevMcp.Tools;
using MedApp.Infrastructure.Common.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;

namespace MedApp.DevMcp.DemoData;

/// <summary>What a member of staff does at the centre, which decides the work they're given.</summary>
public enum StaffKind
{
    ClinicalLead,
    ServiceManager,
    Dermatologist,

    /// <summary>A GP with a special interest in skin.</summary>
    SkinGp,

    /// <summary>The visiting consultant gynaecologist: here two days a week.</summary>
    Gynaecologist,

    /// <summary>The visiting consultant urologist: here a day and a half a week.</summary>
    Urologist,

    /// <summary>The clinical nurse specialist for skin cancer.</summary>
    SkinCancerNurse,

    /// <summary>The clinical nurse specialist for diabetes, part-time.</summary>
    DiabetesNurse,

    /// <summary>The lab: histology, blood and urine tests.</summary>
    BiomedicalScientist,

    /// <summary>The podiatrist who runs the biomechanics service.</summary>
    Podiatrist,
    Pharmacist,
    BookingCoordinator,
    HealthcareAssistant,
    NewStarter,
    Leaver,
}

/// <summary>A staff email address's state: confirmed, waiting to be confirmed, or none.</summary>
public enum EmailState
{
    Confirmed,
    Pending,
    None,
}

/// <summary>One member of staff in the demo: invented, with an initial.surname username.</summary>
public sealed record StaffSeed(
    string Username,
    string Name,
    string JobTitle,
    StaffKind Kind,
    EmailState Email = EmailState.Confirmed);

/// <summary>A member of staff as created: their account's ID and the password they sign in with.</summary>
public sealed class StaffMember(StaffSeed seed, Guid id)
{
    public StaffSeed Seed { get; } = seed;
    public Guid Id { get; } = id;
    public string Username => Seed.Username;
    public StaffKind Kind => Seed.Kind;
    public string? Password { get; set; }
    public PathwayActor Actor => new(Id, Username);

    /// <summary>The account's two-factor key (as typed into an app) and recovery codes, if it has two-factor.</summary>
    public string? TwoFactorKey { get; set; }
    public IReadOnlyList<string>? RecoveryCodes { get; set; }

    /// <summary>From when, and until when, they worked here (UTC); the leaver's account was deleted on their last day.</summary>
    public DateTime StartedUtc { get; set; }
    public DateTime? LeftUtc { get; set; }

    /// <summary>When they became an admin (the clinical lead always was; the service manager was made one).</summary>
    public DateTime? AdminFromUtc { get; set; }

    public bool IsAdminAt(DateTime utc) => AdminFromUtc is { } from && utc >= from;

    /// <summary>The starting role their job gives them from their first day (<see cref="DemoStaff.RoleOf"/>).</summary>
    public StandardRole Role => DemoStaff.RoleOf(Kind);

    /// <summary>A role an admin made for them later, in place of their starting role (the visiting consultants').</summary>
    public (DateTime FromUtc, string Name, IReadOnlySet<Permission> Permissions)? LaterRole { get; set; }

    /// <summary>Their role's name now: the later one, if they were given one, or their starting role.</summary>
    public string RoleName => LaterRole?.Name ?? Role.Name;

    /// <summary>
    /// What their roles allowed at a moment: their job's starting role (or the one given later, from then), and Admin
    /// once they were one.
    /// </summary>
    public IReadOnlySet<Permission> PermissionsAt(DateTime utc)
    {
        var role = LaterRole is { } later && utc >= later.FromUtc ? later.Permissions : Role.Permissions;
        return IsAdminAt(utc) ? Permissions.GrantedBy([role, Permissions.Admin]) : role;
    }

    public bool WorksAt(DateTime utc) => utc >= StartedUtc && (LeftUtc is null || utc < LeftUtc);

    /// <summary>The network address their requests come from: one of the documentation ranges, never a real one.</summary>
    public string IpAddress { get; set; } = "192.0.2.10";

    public ActivityActor ActivityActor => new(Id, Username, IpAddress);

    public string Email => $"{Username}@{DemoStaff.EmailDomain}";
}

/// <summary>The centre's staff, and creating their accounts through the app's own commands.</summary>
public static class DemoStaff
{
    /// <summary>The reserved domain staff email addresses are on (<c>.example</c> is never anyone's).</summary>
    public const string EmailDomain = "centre.example";

    public const string ManagerUsername = "j.whitlock";
    public const string CoordinatorUsername = "h.marsh";
    public const string LockedUsername = "c.dunmore";
    public const string NewStarterUsername = "e.rowntree";
    public const string LeaverUsername = "g.harrow";
    public const string TwoFactorUsername = ManagerUsername;

    public const string DermatologistUsername = "n.thornbury";
    public const string SkinGpUsername = "t.hallworth";
    public const string GynaecologistUsername = "f.linton";
    public const string UrologistUsername = "o.penhale";
    public const string SkinCancerNurseUsername = "b.sandiford";
    public const string DiabetesNurseUsername = "m.carrow";
    public const string ScientistUsername = "r.aldous";
    public const string PodiatristUsername = "s.winterburn";
    public const string PharmacistUsername = "d.quince";

    public const string NoAssignmentNotificationsUsername = GynaecologistUsername;
    public const string PasswordResetUsername = PodiatristUsername;

    /// <summary>Everyone but admin (the kept account, the clinical lead). Invented names.</summary>
    public static readonly StaffSeed[] Roster =
    [
        new(ManagerUsername, "Jo Whitlock", "Service manager", StaffKind.ServiceManager),
        new(DermatologistUsername, "Nadia Thornbury", "Consultant dermatologist", StaffKind.Dermatologist),
        new(SkinGpUsername, "Tom Hallworth", "GP with a special interest in skin", StaffKind.SkinGp),
        new(GynaecologistUsername, "Farah Linton", "Consultant gynaecologist (visiting)", StaffKind.Gynaecologist),
        new(UrologistUsername, "Owen Penhale", "Consultant urologist (visiting)", StaffKind.Urologist),
        new(SkinCancerNurseUsername, "Bea Sandiford", "Clinical nurse specialist: skin cancer", StaffKind.SkinCancerNurse),
        new(DiabetesNurseUsername, "Mina Carrow", "Clinical nurse specialist: diabetes (part-time)", StaffKind.DiabetesNurse),
        new(ScientistUsername, "Ravi Aldous", "Biomedical scientist (the lab)", StaffKind.BiomedicalScientist),
        new(PodiatristUsername, "Sol Winterburn", "Podiatrist: biomechanics", StaffKind.Podiatrist),
        new(PharmacistUsername, "Dara Quince", "Pharmacist", StaffKind.Pharmacist),
        new(CoordinatorUsername, "Hannah Marsh", "Booking coordinator", StaffKind.BookingCoordinator),
        new(LockedUsername, "Chris Dunmore", "Healthcare assistant", StaffKind.HealthcareAssistant, EmailState.None),
        new(NewStarterUsername, "Eden Rowntree", "Staff nurse (new starter)", StaffKind.NewStarter, EmailState.Pending),
        new(LeaverUsername, "Gray Harrow", "GP with a special interest in skin (left)", StaffKind.Leaver),
    ];

    /// <summary>
    /// The starting role each job gets (never "All staff (before roles)"): the doctors, the podiatrist and the pharmacist
    /// Clinician; the nurses and the healthcare assistant Nurse; the booking coordinator Booking; the biomedical
    /// scientist Lab; the service manager Manager. The clinical lead (admin) has Clinician beside Admin.
    /// </summary>
    public static StandardRole RoleOf(StaffKind kind) => kind switch
    {
        StaffKind.ServiceManager => StandardRoles.Manager,
        StaffKind.SkinCancerNurse or StaffKind.DiabetesNurse or StaffKind.HealthcareAssistant or StaffKind.NewStarter => StandardRoles.Nurse,
        StaffKind.BookingCoordinator => StandardRoles.Booking,
        StaffKind.BiomedicalScientist => StandardRoles.Lab,
        _ => StandardRoles.Clinician,
    };

    /// <summary>The kept admin account, as the centre's lead: a consultant physician who's also an admin.</summary>
    public static readonly StaffSeed Lead = new(TestAdminTools.Username, "Clinical lead", "Clinical lead, consultant physician (diabetes)", StaffKind.ClinicalLead);

    /// <summary>
    /// Creates every account but the leaver's deletion and the special cases done later, each at the moment it was
    /// made in the centre's story, through the app's commands: the generated first password (which, as for every
    /// account medapp-dev makes, needn't be changed, except the new starter's), the email address and its
    /// confirmation, and the roles, all recorded in the activity log as the app records them.
    /// </summary>
    public static async Task<List<StaffMember>> CreateAsync(SeedServices services, DemoTimeline timeline, CancellationToken ct)
    {
        var admin = await services.Run(async sp =>
            await sp.GetRequiredService<UserManager<ApplicationUser>>().FindByNameAsync(TestAdminTools.Username))
            ?? throw new McpException("The admin account was not created.");

        var lead = new StaffMember(Lead, admin.Id)
        {
            Password = TestAdminTools.Password,
            StartedUtc = timeline.AppStartUtc.AddDays(-30),
            AdminFromUtc = DateTime.MinValue,
            IpAddress = "192.0.2.11",
        };
        var staff = new List<StaffMember> { lead };

        var address = 20;
        foreach (var seed in Roster)
        {
            var createdAt = timeline.AccountCreatedUtc(seed.Kind);
            services.At(createdAt, lead.ActivityActor);

            var email = seed.Email == EmailState.None ? null : $"{seed.Username}@{EmailDomain}";
            var result = await services.Run(sp => Guards.Validated(() =>
                sp.GetRequiredService<IMediator>().Send(new CreateUserCommand(seed.Username, email, lead.Id, lead.Username), ct)));
            if (!result.Succeeded)
                Guards.Check(result.Result, $"Create {seed.Username}");

            var member = await services.Run(async sp =>
            {
                var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await users.FindByNameAsync(seed.Username) ?? throw new McpException($"{seed.Username} was not created.");
                user.CreatedAt = createdAt;
                // As for every account medapp-dev makes, the generated password can be used straight away; the new
                // starter's, made this week, still has to be changed at first sign-in, as the app asks.
                user.MustChangePassword = seed.Kind == StaffKind.NewStarter;
                Guards.Check(await users.UpdateAsync(user), $"Update {seed.Username}");
                return new StaffMember(seed, user.Id)
                {
                    Password = result.GeneratedPassword,
                    StartedUtc = createdAt,
                    IpAddress = $"192.0.2.{address++}",
                };
            });

            // Roles: the starting role for their job. The service manager is made an admin later (a role change).
            services.At(createdAt.AddMinutes(1), lead.ActivityActor);
            await services.Run(sp => Guards.Validated(() =>
                sp.GetRequiredService<IMediator>().Send(new AssignRoleCommand(seed.Username, RoleOf(seed.Kind).Name), ct)));

            // Most confirm their address from the link the next working morning; the new starter hasn't yet.
            if (seed.Email == EmailState.Confirmed)
            {
                var confirmedAt = createdAt.AddHours(18);
                services.At(confirmedAt, member.ActivityActor);
                await services.Run(async sp =>
                {
                    var request = await sp.GetRequiredService<IAccountEmailService>().RequestChangeAsync(member.Id, email!, ct);
                    if (request?.Token is not { } token)
                        throw new McpException($"Couldn't make a confirmation link for {seed.Username}.");
                    var confirmed = await sp.GetRequiredService<IMediator>().Send(new ConfirmEmailCommand(member.Id, token, member.Id), ct);
                    Guards.Check(confirmed, $"Confirm {seed.Username}'s address");
                });
            }

            staff.Add(member);
        }

        await SpecialCasesAsync(services, staff, timeline, ct);
        return staff;
    }

    /// <summary>
    /// The account histories that show on the Users page and in the activity log: the service manager made an admin,
    /// then turning two-factor on; a password reset by an admin and changed by its owner; someone who turned off
    /// assignment notifications. All through the app's commands, at their moments.
    /// </summary>
    private static async Task SpecialCasesAsync(SeedServices services, List<StaffMember> staff, DemoTimeline timeline, CancellationToken ct)
    {
        var lead = staff[0];
        var manager = Get(staff, ManagerUsername);

        // Promoted to service manager: made an admin (one role change in the activity log).
        services.At(timeline.ManagerMadeAdminUtc, lead.ActivityActor);
        await services.Run(sp => Guards.Validated(() =>
            sp.GetRequiredService<IMediator>().Send(new AssignRoleCommand(ManagerUsername, AppRoles.Admin), ct)));
        manager.AdminFromUtc = timeline.ManagerMadeAdminUtc;

        // Two-factor, as Account Settings does it: a new key, then a code from the app to turn it on.
        services.At(timeline.TwoFactorEnabledUtc, manager.ActivityActor);
        var setup = await services.Run(sp =>
            sp.GetRequiredService<IMediator>().Send(new BeginAuthenticatorSetupCommand(manager.Id), ct));
        services.At(timeline.TwoFactorEnabledUtc.AddMinutes(1), manager.ActivityActor);
        var code = Totp.Compute(Base32.Decode(setup.SharedKey.Replace(" ", string.Empty)), Totp.TimeStep(services.Clock.GetUtcNow()));
        var codes = await services.Run(sp => Guards.Validated(() =>
            sp.GetRequiredService<IMediator>().Send(new EnableTwoFactorCommand(manager.Id, manager.Password!, code, null), ct)));
        manager.TwoFactorKey = setup.SharedKey;
        manager.RecoveryCodes = codes.RecoveryCodes;

        // A forgotten password: reset by an admin, then changed by its owner, as the app makes them.
        var podiatrist = Get(staff, PasswordResetUsername);
        services.At(timeline.PasswordResetUtc, manager.ActivityActor);
        var reset = await services.Run(sp =>
            sp.GetRequiredService<IMediator>().Send(new ResetUserPasswordCommand(PasswordResetUsername), ct));
        var chosen = Passwords.Generate();
        services.At(timeline.PasswordResetUtc.AddHours(1), podiatrist.ActivityActor);
        var changed = await services.Run(sp => Guards.Validated(() =>
            sp.GetRequiredService<IMediator>().Send(new UpdateUserPasswordCommand(PasswordResetUsername, reset.GeneratedPassword!, chosen), ct)));
        Guards.Check(changed, $"Change {PasswordResetUsername}'s password");
        podiatrist.Password = chosen;

        // Turned off being told of assignments (Account Settings).
        var optedOut = Get(staff, NoAssignmentNotificationsUsername);
        services.At(timeline.NotificationsTurnedOffUtc, optedOut.ActivityActor);
        await services.Run(sp =>
            sp.GetRequiredService<IMediator>().Send(new UpdateMyNotificationPreferencesCommand(optedOut.Id, PathwayAssigned: false), ct));

        await DemoRolesAsync(services, staff, timeline, ct);

        foreach (var member in staff)
            if (member.Kind == StaffKind.Leaver)
                member.LeftUtc = timeline.LeaverLeftUtc;
    }

    /// <summary>A role the lead made for the visiting consultants: Clinician's, without overriding stages.</summary>
    public const string VisitingConsultantRole = "Visiting consultant";

    /// <summary>A role made for locums covering clinics, archived once the locums stopped.</summary>
    public const string LocumRole = "Locum cover";

    /// <summary>
    /// Roles shaped to the centre, on the Roles and Permissions page, through the role commands (so they're in the
    /// activity log as the page records them): a role for locums, made and later archived; and a role of their own for
    /// the visiting consultants, who then hold it in place of Clinician.
    /// </summary>
    private static async Task DemoRolesAsync(SeedServices services, List<StaffMember> staff, DemoTimeline timeline, CancellationToken ct)
    {
        var lead = staff[0];

        services.At(timeline.LocumRoleCreatedUtc, lead.ActivityActor);
        var locum = await FreshRoleAsync(services, lead, LocumRole,
            "Locums covering a clinic: patients, their pathways, appointments and files.",
            [Permission.ViewPatients, Permission.WorkPathways, Permission.BookAppointments, Permission.AttachFiles], ct);

        services.At(timeline.RolesRefinedUtc, lead.ActivityActor);
        await services.Run(sp => Guards.Validated(() => sp.GetRequiredService<IMediator>().Send(
            new SetRoleArchivedCommand(locum.Id, true, locum.Version, lead.Id, lead.Username), ct)));

        var visiting = StandardRoles.Clinician.Permissions
            .Where(p => p is not (Permission.UnlockStages or Permission.FinishWithoutVisit)).ToHashSet();
        services.At(timeline.RolesRefinedUtc.AddMinutes(5), lead.ActivityActor);
        await FreshRoleAsync(services, lead, VisitingConsultantRole,
            "Consultants who visit for clinics: as Clinician, but they don't unlock stages or finish one without its visit.",
            visiting, ct);

        var minute = 10;
        foreach (var username in new[] { GynaecologistUsername, UrologistUsername })
        {
            var at = timeline.RolesRefinedUtc.AddMinutes(minute++);
            services.At(at, lead.ActivityActor);
            var result = await services.Run(sp => Guards.Validated(() => sp.GetRequiredService<IMediator>().Send(
                new SetUserRolesCommand(username, [VisitingConsultantRole], lead.Username), ct)));
            Guards.Check(result, $"Give {username} the {VisitingConsultantRole} role");
            Get(staff, username).LaterRole = (at, VisitingConsultantRole, visiting);
        }
    }

    /// <summary>
    /// Makes a role through the role command, first deleting one with its name from an earlier run (the wipe leaves
    /// roles), so each run tells the same story.
    /// </summary>
    private static async Task<RoleResponse> FreshRoleAsync(
        SeedServices services, StaffMember lead, string name, string description, IEnumerable<Permission> permissions, CancellationToken ct)
    {
        await services.Run(async sp =>
        {
            var roles = sp.GetRequiredService<IRoleRepository>();
            var earlier = (await roles.GetAllAsync(ct)).FirstOrDefault(r => !r.IsBuiltIn && string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
            if (earlier is not null && !await roles.DeleteIfUnheldAsync(earlier.Id, ct))
                throw new McpException($"A kept account holds the role {name}: take it from them, then reset again.");
        });

        return await services.Run(sp => Guards.Validated(() => sp.GetRequiredService<IMediator>().Send(
            new CreateRoleCommand(name, description, permissions.Select(p => p.ToString()).ToList(), lead.Id, lead.Username), ct)));
    }

    public static StaffMember Get(IEnumerable<StaffMember> staff, string username) =>
        staff.Single(s => s.Username == username);

    public static StaffMember? OfKind(IEnumerable<StaffMember> staff, StaffKind kind) =>
        staff.FirstOrDefault(s => s.Kind == kind);
}
