using MediatR;
using MedApp.Application.Activity.Common;
using MedApp.Application.Authentication.Emails.Commands.ConfirmEmail;
using MedApp.Application.Authentication.Roles.Commands.AssignRole;
using MedApp.Application.Authentication.Users.Commands.CreateUser;
using MedApp.Application.Authentication.Users.Commands.SetStaffDetails;
using MedApp.Application.Authentication.Users.Commands.SetUserTeams;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Pathways.Common;
using MedApp.DevMcp.Tools;
using MedApp.Domain.Teams;
using MedApp.Infrastructure.Common.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// Someone in the demo clinic, as the light demo has them (<c>MedApp.Web/src/demo/accounts.ts</c>): their username,
/// name, job title, roles, and teams with their role in each. Their actor carries what their roles allow and their team
/// walls, as a request's would, so the handlers the seed calls refuse what the app would refuse them.
/// </summary>
public sealed class DemoPerson(string username, string fullName, string jobTitle, StandardRole[] roles, params (string Team, TeamMemberRole Role)[] teams)
{
    public string Username { get; } = username;
    public string FullName { get; } = fullName;
    public string JobTitle { get; } = jobTitle;
    public IReadOnlyList<StandardRole> Roles { get; } = roles;
    public IReadOnlyList<(string Team, TeamMemberRole Role)> Teams { get; } = teams;

    public Guid Id { get; set; }
    public string? Password { get; set; }

    /// <summary>The network address their requests come from: one of the documentation ranges, never a real one.</summary>
    public string IpAddress { get; set; } = "192.0.2.10";

    /// <summary>Their teams' IDs, once the teams are made.</summary>
    public List<Guid> TeamIds { get; } = [];

    public bool IsAdmin => Roles.Contains(StandardRoles.Admin);

    /// <summary>What their roles allow: Admin's fixed set, or their roles' together (every role grants UseApp).</summary>
    public IReadOnlySet<Permission> Permissions =>
        Application.Common.Identity.Permissions.GrantedBy(Roles.Select(role => role == StandardRoles.Admin
            ? Application.Common.Identity.Permissions.Admin
            : role.Permissions.Append(Permission.UseApp).ToHashSet()));

    /// <summary>Their team walls, as their session's.</summary>
    public TeamScope Scope => TeamScope.For(Id, TeamIds, Permissions);

    public PathwayActor Actor => new(Id, Username, Permissions, Scope);

    public CalendarActor Calendar => new(Id, Username, Permissions, Scope);

    public ActivityActor ActivityActor => new(Id, Username, IpAddress);

    public string Email => $"{Username}@{DemoPeople.EmailDomain}";
}

/// <summary>
/// The demo clinic's people (the light demo's <c>PEOPLE</c>): the admin (read only, in no team), Dermatology's clinic
/// admin, the lab's pathologist, the Eye clinic's clinic admin, and the rest of the centre. Where the team walls need it,
/// they're in the teams they work for: the booking coordinator books and registers both clinics' work, and the service
/// manager assigns their pathways (both in no team in the light demo, where nothing checks it).
/// </summary>
public static class DemoPeople
{
    /// <summary>The reserved domain staff email addresses are on (<c>.example</c> is never anyone's).</summary>
    public const string EmailDomain = "centre.example";

    public const string Dermatology = "Dermatology";
    public const string EyeClinic = "Eye clinic";

    public static DemoPerson Admin { get; private set; } = null!;
    public static DemoPerson Thornbury { get; private set; } = null!;
    public static DemoPerson Aldous { get; private set; } = null!;
    public static DemoPerson Whitlock { get; private set; } = null!;
    public static DemoPerson Sandiford { get; private set; } = null!;
    public static DemoPerson Marsh { get; private set; } = null!;
    public static DemoPerson Hallworth { get; private set; } = null!;
    public static DemoPerson Varma { get; private set; } = null!;
    public static DemoPerson Ferrand { get; private set; } = null!;

    /// <summary>Everyone, the admin first (fresh each run: the seed fills in their IDs and passwords).</summary>
    public static IReadOnlyList<DemoPerson> Roster()
    {
        Admin = new DemoPerson(TestAdminTools.Username, "Centre administrator", "Centre administrator", [StandardRoles.Admin]) { IpAddress = "192.0.2.11" };
        Thornbury = new DemoPerson("n.thornbury", "Nadia Thornbury", "Consultant dermatologist", [StandardRoles.Clinician],
            (Dermatology, TeamMemberRole.ClinicAdmin)) { IpAddress = "192.0.2.21" };
        Aldous = new DemoPerson("r.aldous", "Ravi Aldous", "Consultant histopathologist", [StandardRoles.Lab, StandardRoles.Pathologist],
            (Team.LabName, TeamMemberRole.Member)) { IpAddress = "192.0.2.22" };
        Whitlock = new DemoPerson("j.whitlock", "Jo Whitlock", "Service manager", [StandardRoles.Manager],
            (Dermatology, TeamMemberRole.Member), (EyeClinic, TeamMemberRole.Member)) { IpAddress = "192.0.2.23" };
        Sandiford = new DemoPerson("b.sandiford", "Bea Sandiford", "Clinical nurse specialist: skin cancer", [StandardRoles.Nurse],
            (Dermatology, TeamMemberRole.Member)) { IpAddress = "192.0.2.24" };
        Marsh = new DemoPerson("h.marsh", "Hannah Marsh", "Booking coordinator", [StandardRoles.Booking],
            (Dermatology, TeamMemberRole.Member), (EyeClinic, TeamMemberRole.Member)) { IpAddress = "192.0.2.25" };
        Hallworth = new DemoPerson("t.hallworth", "Tom Hallworth", "GP with an extended role (GPwER) in dermatology", [StandardRoles.Clinician],
            (Dermatology, TeamMemberRole.Member)) { IpAddress = "192.0.2.26" };
        Varma = new DemoPerson("s.varma", "Saira Varma", "Consultant ophthalmologist", [StandardRoles.Clinician],
            (EyeClinic, TeamMemberRole.ClinicAdmin)) { IpAddress = "192.0.2.27" };
        Ferrand = new DemoPerson("c.ferrand", "Clara Ferrand", "Ophthalmic nurse", [StandardRoles.Nurse],
            (EyeClinic, TeamMemberRole.Member)) { IpAddress = "192.0.2.28" };
        return [Admin, Thornbury, Aldous, Whitlock, Sandiford, Marsh, Hallworth, Varma, Ferrand];
    }

    /// <summary>
    /// The admin's account as the demo has it: Admin alone (read only), named. The kept account comes back from
    /// <c>ensure_test_admin</c> with Clinician too, which the demo's admin doesn't hold.
    /// </summary>
    public static async Task SetUpAdminAsync(DemoSeed seed)
    {
        var admin = Admin;
        await seed.Services.Run(async sp =>
        {
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByNameAsync(admin.Username) ?? throw new McpException("The admin account was not created.");
            admin.Id = user.Id;
            var extra = (await users.GetRolesAsync(user)).Except([AppRoles.Admin], StringComparer.OrdinalIgnoreCase).ToList();
            if (extra.Count > 0)
                Guards.Check(await users.RemoveFromRolesAsync(user, extra), "Set the admin account's roles");
            user.CreatedAt = seed.Timeline.Ago(200, 9, 0);
            // Kept through the wipe: cleared, so naming them below is recorded each time (the same story every run).
            user.FullName = null;
            user.JobTitle = null;
            Guards.Check(await users.UpdateAsync(user), "Date the admin account");
        });
        admin.Password = TestAdminTools.Password;
        await seed.Send(seed.Timeline.Ago(200, 9, 1), admin, new SetStaffDetailsCommand(admin.Username, admin.FullName, admin.JobTitle), "Name the admin");
    }

    /// <summary>
    /// Makes everyone else's account through the app's commands, as the admin did: the account with its generated
    /// password (which, as for every account medapp-dev makes, needn't be changed), its roles, the name and job title,
    /// and the email address, confirmed from the link the next morning.
    /// </summary>
    public static async Task CreateAccountsAsync(DemoSeed seed)
    {
        var admin = Admin;
        var at = seed.Timeline.Ago(130, 9, 0);
        foreach (var person in seed.People.Where(p => !p.IsAdmin))
        {
            at = at.AddMinutes(5);
            var created = await seed.Send(at, admin, new CreateUserCommand(person.Username, person.Email, admin.Id, admin.Username), $"Create {person.Username}");
            if (!created.Succeeded)
                Guards.Check(created.Result, $"Create {person.Username}");
            person.Password = created.GeneratedPassword;

            var createdAt = at;
            await seed.Services.Run(async sp =>
            {
                var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await users.FindByNameAsync(person.Username) ?? throw new McpException($"{person.Username} was not created.");
                user.CreatedAt = createdAt;
                user.MustChangePassword = false;
                Guards.Check(await users.UpdateAsync(user), $"Update {person.Username}");
                person.Id = user.Id;
            });

            foreach (var role in person.Roles)
                await seed.Send(at.AddMinutes(1), admin, new AssignRoleCommand(person.Username, role.Name), $"Give {person.Username} {role.Name}");
            await seed.Send(at.AddMinutes(2), admin, new SetStaffDetailsCommand(person.Username, person.FullName, person.JobTitle), $"Name {person.Username}");

            var confirmedAt = at.AddHours(18);
            seed.Services.At(confirmedAt, person.ActivityActor);
            await seed.Services.Run(async sp =>
            {
                var request = await sp.GetRequiredService<IAccountEmailService>().RequestChangeAsync(person.Id, person.Email, seed.Ct);
                if (request?.Token is not { } token)
                    throw new McpException($"Couldn't make a confirmation link for {person.Username}.");
                var confirmed = await sp.GetRequiredService<IMediator>().Send(new ConfirmEmailCommand(person.Id, token, person.Id), seed.Ct);
                Guards.Check(confirmed, $"Confirm {person.Username}'s address");
            });
        }
    }

    /// <summary>Puts everyone in their teams, with their role in each, as the admin did once the teams were made.</summary>
    public static async Task AddMembershipsAsync(DemoSeed seed, DateTime at)
    {
        foreach (var person in seed.People.Where(p => p.Teams.Count > 0))
        {
            at = at.AddMinutes(1);
            var choices = person.Teams
                .Select(t => new TeamChoice(seed.TeamId(t.Team), t.Role == TeamMemberRole.ClinicAdmin ? nameof(TeamMemberRole.ClinicAdmin) : null))
                .ToList();
            await seed.Send(at, Admin, new SetUserTeamsCommand(person.Username, choices), $"Put {person.Username} in their teams");
            person.TeamIds.Clear();
            person.TeamIds.AddRange(choices.Select(c => c.TeamId));
        }
    }
}
