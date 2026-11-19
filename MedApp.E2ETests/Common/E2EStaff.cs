using MedApp.Application.Common.Identity;
using MedApp.Contracts.Teams.Responses;

namespace MedApp.E2ETests.Common;

/// <summary>Who a test's page and <c>Api</c> are signed in as.</summary>
public enum E2EAccount
{
    /// <summary>The run's member of staff (<see cref="E2EStaff"/>): clinical work, in the E2E clinic.</summary>
    Staff,

    /// <summary>The admin from <c>E2E_USERNAME</c>: users, roles and setup. Admins are read only for clinical work.</summary>
    Admin,
}

/// <summary>
/// The member of staff the run makes (<see cref="E2ESession"/>), who does every journey's clinical work: admins are read
/// only, and clinical records sit behind team walls. They hold the run's own role (<see cref="Permissions"/>) and belong
/// to the E2E clinic (<see cref="Team"/>), where the tests put their templates and referrals.
/// </summary>
/// <param name="Password">Chosen for the run (<see cref="E2EData.NewPassword"/>); never in a message.</param>
public sealed record E2EStaff(string Username, string Password, Guid UserId, string FullName, TeamResponse Team, Guid RoleId)
{
    /// <summary>The clinic team every run uses: made by the first run, restored if archived, never deleted (README).</summary>
    public const string TeamName = "E2E clinic (synthetic)";

    /// <summary>
    /// The run's role: Clinician's permissions, with what the journeys' clean-ups need (deleting patients and pathways)
    /// and assigning pathways (My Worklist). No standard role deletes a pathway, so the run makes its own.
    /// </summary>
    public static readonly IReadOnlyList<string> Permissions =
    [
        .. StandardRoles.Clinician.Permissions
            .Append(Permission.DeletePatients)
            .Append(Permission.DeletePathways)
            .Append(Permission.AssignPathways)
            .Where(p => p != Permission.UseApp)
            .Distinct()
            .Order()
            .Select(p => p.ToString()),
    ];
}
