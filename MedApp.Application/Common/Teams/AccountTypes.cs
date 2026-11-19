using MedApp.Application.Common.Identity;
using MedApp.Domain.Teams;

namespace MedApp.Application.Common.Teams;

/// <summary>
/// Account types (decision 182 of <c>docs/multi-clinic-plan.md</c>), worked out, never stored: <b>Admin</b> holds the
/// built-in Admin role (overview, Insights, read-only access to all data, accounts and setup; no clinical changes);
/// <b>ClinicAdmin</b> is staff who is the clinic admin of any of their teams; anyone else is <b>Staff</b>. Sent by name
/// as <c>accountType</c> on whoami, each user and Account Settings; the web app's copy is <c>src/auth/accountTypes.ts</c>.
/// </summary>
public static class AccountTypes
{
    public const string Admin = "Admin";
    public const string ClinicAdmin = "ClinicAdmin";
    public const string Staff = "Staff";

    /// <summary>The type from someone's roles and their roles in their teams: Admin first, then clinic admin.</summary>
    public static string Of(IEnumerable<string> roles, IEnumerable<TeamMemberRole> teamRoles)
    {
        if (roles.Contains(AppRoles.Admin, StringComparer.Ordinal))
            return Admin;
        return teamRoles.Contains(TeamMemberRole.ClinicAdmin) ? ClinicAdmin : Staff;
    }
}
