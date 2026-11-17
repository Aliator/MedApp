using MediatR;
using MedApp.Application.Activity.Common;
using MedApp.Application.Authentication.Emails.Commands.ConfirmEmail;
using MedApp.Application.Authentication.Roles.Commands.AssignRole;
using MedApp.Application.Authentication.TwoFactor.Commands.BeginAuthenticatorSetup;
using MedApp.Application.Authentication.TwoFactor.Commands.EnableTwoFactor;
using MedApp.Application.Authentication.Users.Commands.CreateUser;
using MedApp.Application.Authentication.Users.Commands.ResetUserPassword;
using MedApp.Application.Authentication.Users.Commands.UpdateUserPassword;
using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Commands.UpdateMyNotificationPreferences;
using MedApp.Application.Procedures.Common;
using MedApp.DevMcp.Tools;
using MedApp.Infrastructure.Common.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;

namespace MedApp.DevMcp.DemoData;

/// <summary>What a member of staff does at the clinic, which decides the work they're given.</summary>
public enum StaffKind
{
    ClinicalLead,
    ServiceManager,
    Surgeon,
    Endoscopist,
    Nurse,
    MinorProceduresNurse,
    PartTimeNurse,
    Physiotherapist,
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
    string WeekTemplate,
    EmailState Email = EmailState.Confirmed);

/// <summary>A member of staff as created: their account's ID and the password they sign in with.</summary>
public sealed class StaffMember(StaffSeed seed, Guid id)
{
    public StaffSeed Seed { get; } = seed;
    public Guid Id { get; } = id;
    public string Username => Seed.Username;
    public StaffKind Kind => Seed.Kind;
    public string? Password { get; set; }
    public ProcedureActor Actor => new(Id, Username);

    /// <summary>The account's two-factor key (as typed into an app) and recovery codes, if it has two-factor.</summary>
    public string? TwoFactorKey { get; set; }
    public IReadOnlyList<string>? RecoveryCodes { get; set; }

    /// <summary>From when, and until when, they worked here (UTC); the leaver's account was deleted on their last day.</summary>
    public DateTime StartedUtc { get; set; }
    public DateTime? LeftUtc { get; set; }

    /// <summary>When they became an admin (the clinical lead always was; the service manager was made one).</summary>
    public DateTime? AdminFromUtc { get; set; }

    public bool IsAdminAt(DateTime utc) => AdminFromUtc is { } from && utc >= from;

    public bool WorksAt(DateTime utc) => utc >= StartedUtc && (LeftUtc is null || utc < LeftUtc);

    /// <summary>The network address their requests come from: one of the documentation ranges, never a real one.</summary>
    public string IpAddress { get; set; } = "192.0.2.10";

    public ActivityActor ActivityActor => new(Id, Username, IpAddress);

    public string Email => $"{Username}@clinic.example";
}

/// <summary>The clinic's staff, and creating their accounts through the app's own commands.</summary>
public static class DemoStaff
{
    public const string ManagerUsername = "j.whitlock";
    public const string CoordinatorUsername = "h.marsh";
    public const string LockedUsername = "c.dunmore";
    public const string NewStarterUsername = "e.rowntree";
    public const string LeaverUsername = "g.harrow";
    public const string TwoFactorUsername = ManagerUsername;
    public const string NoAssignmentNotificationsUsername = "m.fairbanks";
    public const string PasswordResetUsername = "a.pemberton";

    /// <summary>Everyone but admin (the kept account, the clinical lead). Invented names.</summary>
    public static readonly StaffSeed[] Roster =
    [
        new(ManagerUsername, "Jo Whitlock", "Service manager", StaffKind.ServiceManager, "Office"),
        new("r.calloway", "Rhian Calloway", "Consultant surgeon", StaffKind.Surgeon, "Surgeon"),
        new("d.ashworth", "Dev Ashworth", "Consultant surgeon", StaffKind.Surgeon, "Surgeon"),
        new(NoAssignmentNotificationsUsername, "Maya Fairbanks", "Consultant gastroenterologist", StaffKind.Endoscopist, "Endoscopy"),
        new("k.ellison", "Kerry Ellison", "Nurse: pre-assessment and recovery", StaffKind.Nurse, "Nursing"),
        new("s.brightwell", "Sam Brightwell", "Nurse: minor procedures clinic", StaffKind.MinorProceduresNurse, "Nursing"),
        new("l.northcott", "Leah Northcott", "Nurse: wound care and follow-up (part-time)", StaffKind.PartTimeNurse, "Part-time (mornings)"),
        new(PasswordResetUsername, "Alex Pemberton", "Physiotherapist", StaffKind.Physiotherapist, "Physiotherapy"),
        new(CoordinatorUsername, "Hannah Marsh", "Booking coordinator", StaffKind.BookingCoordinator, "Office"),
        new(LockedUsername, "Chris Dunmore", "Healthcare assistant", StaffKind.HealthcareAssistant, "Nursing", EmailState.None),
        new(NewStarterUsername, "Eden Rowntree", "Staff nurse (new starter)", StaffKind.NewStarter, "Nursing", EmailState.Pending),
        new(LeaverUsername, "Gray Harrow", "Consultant surgeon (left)", StaffKind.Leaver, "Surgeon"),
    ];

    /// <summary>The kept admin account, as the clinic's lead: a consultant surgeon who's also an admin.</summary>
    public static readonly StaffSeed Lead = new(TestAdminTools.Username, "Clinical lead", "Clinical lead, consultant surgeon", StaffKind.ClinicalLead, "Clinical lead");

    /// <summary>
    /// Creates every account but the leaver's deletion and the special cases done later, each at the moment it was
    /// made in the clinic's story, through the app's commands: the generated first password (which, as for every
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

            var email = seed.Email == EmailState.None ? null : $"{seed.Username}@clinic.example";
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

            // Roles: everyone is staff (User). The service manager is made an admin later (a role change).
            services.At(createdAt.AddMinutes(1), lead.ActivityActor);
            await services.Run(sp => Guards.Validated(() =>
                sp.GetRequiredService<IMediator>().Send(new AssignRoleCommand(seed.Username, "User"), ct)));

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
            sp.GetRequiredService<IMediator>().Send(new AssignRoleCommand(ManagerUsername, "Admin"), ct)));
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
        var physio = Get(staff, PasswordResetUsername);
        services.At(timeline.PasswordResetUtc, manager.ActivityActor);
        var reset = await services.Run(sp =>
            sp.GetRequiredService<IMediator>().Send(new ResetUserPasswordCommand(PasswordResetUsername), ct));
        var chosen = Passwords.Generate();
        services.At(timeline.PasswordResetUtc.AddHours(1), physio.ActivityActor);
        var changed = await services.Run(sp => Guards.Validated(() =>
            sp.GetRequiredService<IMediator>().Send(new UpdateUserPasswordCommand(PasswordResetUsername, reset.GeneratedPassword!, chosen), ct)));
        Guards.Check(changed, $"Change {PasswordResetUsername}'s password");
        physio.Password = chosen;

        // Turned off being told of assignments (Account Settings).
        var endoscopist = Get(staff, NoAssignmentNotificationsUsername);
        services.At(timeline.NotificationsTurnedOffUtc, endoscopist.ActivityActor);
        await services.Run(sp =>
            sp.GetRequiredService<IMediator>().Send(new UpdateMyNotificationPreferencesCommand(endoscopist.Id, ProcedureAssigned: false), ct));

        foreach (var member in staff)
            if (member.Kind == StaffKind.Leaver)
                member.LeftUtc = timeline.LeaverLeftUtc;
    }

    public static StaffMember Get(IEnumerable<StaffMember> staff, string username) =>
        staff.Single(s => s.Username == username);

    public static StaffMember? OfKind(IEnumerable<StaffMember> staff, StaffKind kind) =>
        staff.FirstOrDefault(s => s.Kind == kind);
}
