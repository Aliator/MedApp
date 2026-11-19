using MedApp.Application.Common.Teams;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Domain.Teams;

namespace MedApp.Application.Authentication.Users.Common;

/// <summary>Adds each user's account type (<see cref="AccountTypes"/>), from their roles and their teams' roles.</summary>
public static class UserAccountTypes
{
    public static async Task<IReadOnlyList<UserResponse>> WithAccountTypesAsync(
        IReadOnlyList<UserResponse> users, ITeamRepository teams, CancellationToken ct)
    {
        if (users.Count == 0)
            return users;

        var clinicAdmins = await teams.GetClinicAdminsAsync(users.Select(u => u.UserId), ct);
        return users.Select(u => With(u, clinicAdmins.Contains(u.UserId))).ToList();
    }

    public static async Task<UserResponse> WithAccountTypeAsync(UserResponse user, ITeamRepository teams, CancellationToken ct) =>
        (await WithAccountTypesAsync([user], teams, ct))[0];

    private static UserResponse With(UserResponse user, bool isClinicAdmin) =>
        user with
        {
            AccountType = AccountTypes.Of(user.Roles, isClinicAdmin ? [TeamMemberRole.ClinicAdmin] : [])
        };
}
