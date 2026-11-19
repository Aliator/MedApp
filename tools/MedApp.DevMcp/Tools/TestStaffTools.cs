using System.ComponentModel;
using System.Text.RegularExpressions;
using MediatR;
using MedApp.Application.Authentication.Roles.Commands.AssignRole;
using MedApp.Application.Authentication.Roles.Commands.RevokeRole;
using MedApp.Application.Authentication.Users.Commands.SetStaffDetails;
using MedApp.Application.Authentication.Users.Commands.SetUserTeams;
using MedApp.Application.Common.Identity;
using MedApp.Application.Teams.Commands.CreateTeam;
using MedApp.Application.Teams.Commands.SetTeamArchived;
using MedApp.Domain.Teams;
using MedApp.Infrastructure.Common.Identity;
using MedApp.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace MedApp.DevMcp.Tools;

/// <summary>Which team a test account works in.</summary>
public enum TestStaffTeam
{
    /// <summary>The Dermatology clinic, the light demo's (made if the database has none).</summary>
    Dermatology,

    /// <summary>The lab team every database has.</summary>
    Lab,
}

/// <summary>
/// A test account <c>ensure_test_staff</c> makes: its username after the prefix, its name and job, its roles, and its
/// team with its role there.
/// </summary>
public sealed record TestStaffAccount(
    string Suffix,
    string FullName,
    string JobTitle,
    IReadOnlyList<StandardRole> Roles,
    TestStaffTeam Team,
    TeamMemberRole TeamRole)
{
    public string UsernameFor(string prefix) => $"{prefix}-{Suffix}";

    /// <summary>What its roles allow together (every role grants UseApp).</summary>
    public IReadOnlySet<Permission> Permissions =>
        Application.Common.Identity.Permissions.GrantedBy(Roles.Select(r => r.Permissions.Append(Permission.UseApp).ToHashSet()));
}

/// <summary>
/// Staff accounts for local tests and the UI parity screenshots (MC3 of <c>docs/multi-clinic-plan.md</c>): admins are
/// read only, and clinical records sit behind team walls, so clinical work needs a member of staff in the right team.
/// Everything but the password goes through the app's own commands (roles, staff details, team memberships), as
/// <c>create_user</c> does.
/// </summary>
[McpServerToolType]
public sealed class TestStaffTools
{
    public const string DefaultPrefix = "dev";

    public const string DermatologyName = "Dermatology";

    private static readonly Regex PrefixPattern = new("^[a-z][a-z0-9-]{0,30}$");

    /// <summary>
    /// The accounts, as the light demo's <c>n.thornbury</c> and <c>r.aldous</c>: a clinician who is Dermatology's clinic
    /// admin (with Booking, so one account reaches every clinic screen: deleting a patient needs it), and a pathologist
    /// in the lab.
    /// </summary>
    public static readonly IReadOnlyList<TestStaffAccount> Accounts =
    [
        new("clinician", "Dev Clinician (synthetic)", "Consultant dermatologist (synthetic)",
            [StandardRoles.Clinician, StandardRoles.Booking], TestStaffTeam.Dermatology, TeamMemberRole.ClinicAdmin),
        new("lab", "Dev Pathologist (synthetic)", "Consultant histopathologist (synthetic)",
            [StandardRoles.Lab, StandardRoles.Pathologist], TestStaffTeam.Lab, TeamMemberRole.Member),
    ];

    [McpServerTool(Name = "ensure_test_staff", Idempotent = true)]
    [Description("Makes sure the local test staff accounts exist, for clinical work in tests and the UI parity screenshots (admins are read only, and records sit behind team walls): <prefix>-clinician (Clinician and Booking, Dermatology's clinic admin) and <prefix>-lab (Lab and Pathologist, in the lab team). Creates each (and Dermatology, or any missing standard role) if missing; otherwise sets its password, unlocks it, and puts its roles, name and team back exactly. The password is the one given, or a new generated one each, returned either way; the accounts sign in with it straight away, never made to change it. Run it again after reset_demo_data, which remakes the teams. Local dev only.")]
    public static async Task<object> EnsureTestStaff(
        IMediator mediator,
        UserManager<ApplicationUser> users,
        IIdentityTransactions transactions,
        MedAppDbContext context,
        DevMcpOptions options,
        [Description("Start of the usernames, e.g. \"dev\" (dev-clinician, dev-lab) or \"dev-parity\" for the UI parity tool's own. Lower-case letters, digits and hyphens.")]
        string prefix = DefaultPrefix,
        [Description("Password for both accounts. Omit to generate one for each (returned in the result).")]
        string? password = null,
        CancellationToken ct = default)
    {
        var usernames = Usernames(prefix);
        foreach (var username in usernames)
            Guards.RequireNotProtected(options, username, "make a test account of");
        if (password is not null)
            foreach (var username in usernames)
                Guards.Check(await Passwords.Validate(users, username, password), "Check password");

        // Missing standard roles are added (Pathologist came with the lab); ones an admin has edited are left as they are.
        await StandardRoleSetup.EnsureAsync(context, DateTime.UtcNow, resetPermissions: false, ct);
        var lab = await context.Teams.AsNoTracking()
                      .Where(t => t.Kind == TeamKind.Lab && !t.IsArchived)
                      .Select(t => new { t.Id, t.Name })
                      .FirstOrDefaultAsync(ct)
                  ?? throw new McpException("The database has no lab team that isn't archived. Apply the migrations (tools/seed-demo-data.sh) first.");
        var teams = new Dictionary<TestStaffTeam, (Guid Id, string Name)>
        {
            [TestStaffTeam.Dermatology] = (await EnsureDermatologyAsync(mediator, context, ct), DermatologyName),
            [TestStaffTeam.Lab] = (lab.Id, lab.Name),
        };

        var result = new List<object>();
        foreach (var account in Accounts)
        {
            var username = account.UsernameFor(prefix);
            var roles = account.Roles.Select(r => r.Name).ToList();
            var user = await users.FindByNameAsync(username);
            var created = user is null;
            string signInWith;
            if (user is null)
            {
                signInWith = (await AccountTools.CreateAccount(mediator, users, transactions, username, roles, password, ct)).Password;
            }
            else
            {
                signInWith = password ?? Passwords.Generate();
                await ResetPasswordAsync(users, user, signInWith);
                var (add, remove) = RoleChanges(await users.GetRolesAsync(user), roles);
                foreach (var role in add) await Guards.Validated(() => mediator.Send(new AssignRoleCommand(username, role), ct));
                foreach (var role in remove) await Guards.Validated(() => mediator.Send(new RevokeRoleCommand(username, role), ct));
            }

            await Guards.Validated(() => mediator.Send(new SetStaffDetailsCommand(username, account.FullName, account.JobTitle), ct));
            await Guards.Validated(() => mediator.Send(new SetUserTeamsCommand(username, [Choice(account, teams[account.Team].Id)]), ct));

            result.Add(new
            {
                username,
                password = signInWith,
                created,
                roles,
                team = teams[account.Team].Name,
                teamRole = account.TeamRole.ToString(),
            });
        }

        return result;
    }

    /// <summary>The accounts' usernames for a prefix; a prefix that isn't lower-case letters, digits and hyphens is refused.</summary>
    public static IReadOnlyList<string> Usernames(string prefix)
    {
        if (!PrefixPattern.IsMatch(prefix ?? string.Empty))
            throw new McpException($"'{prefix}' isn't a usable prefix: start with a lower-case letter, then lower-case letters, digits or hyphens.");
        return [.. Accounts.Select(a => a.UsernameFor(prefix!))];
    }

    /// <summary>The roles to give and take away so an account holds exactly <paramref name="wanted"/> (names ignore case).</summary>
    public static (IReadOnlyList<string> Add, IReadOnlyList<string> Remove) RoleChanges(IEnumerable<string> current, IEnumerable<string> wanted)
    {
        var have = current.ToList();
        var want = wanted.ToList();
        return (
            [.. want.Except(have, StringComparer.OrdinalIgnoreCase)],
            [.. have.Except(want, StringComparer.OrdinalIgnoreCase)]);
    }

    /// <summary>An account's team membership, as <c>PUT …/teams</c> takes it (a member's role is left out: Member).</summary>
    public static TeamChoice Choice(TestStaffAccount account, Guid teamId) =>
        new(teamId, account.TeamRole == TeamMemberRole.ClinicAdmin ? nameof(TeamMemberRole.ClinicAdmin) : null);

    /// <summary>Dermatology: the clinic team of that name, restored if archived, or made through the app's command.</summary>
    private static async Task<Guid> EnsureDermatologyAsync(IMediator mediator, MedAppDbContext context, CancellationToken ct)
    {
        var team = await context.Teams.AsNoTracking()
            .Where(t => t.Kind == TeamKind.Clinic && t.Name == DermatologyName)
            .Select(t => new { t.Id, t.IsArchived, t.Version })
            .FirstOrDefaultAsync(ct);
        if (team is null)
            return (await Guards.Validated(() => mediator.Send(new CreateTeamCommand(DermatologyName), ct))).Id;
        if (team.IsArchived)
            await Guards.Validated(() => mediator.Send(new SetTeamArchivedCommand(team.Id, false, team.Version), ct));
        return team.Id;
    }

    /// <summary>
    /// Sets the password as <c>set_password</c> does: chosen here, so not to be changed at first sign-in; and unlocks the
    /// account.
    /// </summary>
    private static async Task ResetPasswordAsync(UserManager<ApplicationUser> users, ApplicationUser user, string password)
    {
        user.MustChangePassword = false;
        var token = await users.GeneratePasswordResetTokenAsync(user);
        Guards.Check(await users.ResetPasswordAsync(user, token, password), $"Set {user.UserName}'s password");
        Guards.Check(await users.SetLockoutEndDateAsync(user, null), $"Unlock {user.UserName}");
        Guards.Check(await users.ResetAccessFailedCountAsync(user), $"Reset {user.UserName}'s failed sign-ins");
    }
}
