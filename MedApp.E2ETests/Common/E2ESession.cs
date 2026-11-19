using MedApp.E2ETests.Common;
using Microsoft.Playwright;

// In the root namespace, so it runs once before every test in the assembly (a SetUpFixture covers only its own
// namespace and those under it).
// ReSharper disable once CheckNamespace
namespace MedApp.E2ETests;

/// <summary>
/// Once per test run: signs the admin in (<c>E2E_USERNAME</c>), then, as the admin, makes the run's member of staff
/// (<see cref="Staff"/>), who does the clinical work, as admins are read only: the E2E clinic team (found or made), a
/// role of the run's own, and an account in that team with a password of its own. Both sessions are saved for the
/// tests to reuse. When the run ends both are signed out, and the account and role deleted (the team stays).
/// It only runs when the run contains tests in this assembly.
/// </summary>
[SetUpFixture]
public sealed class E2ESession
{
    /// <summary>The run's member of staff, once <see cref="SignInAsync()"/> has made them.</summary>
    public static E2EStaff Staff { get; private set; } = null!;

    /// <summary>What the set-up made, for the tear-down to delete (also after a set-up that failed part way).</summary>
    private static string? _staffUsername;
    private static Guid? _roleId;

    [OneTimeSetUp]
    public async Task SignInAsync()
    {
        var adminUsername = E2ESettings.Username;
        var adminPassword = E2ESettings.Password;
        if (string.IsNullOrEmpty(adminUsername) || string.IsNullOrEmpty(adminPassword))
        {
            const string message = "Set E2E_USERNAME and E2E_PASSWORD to run the end-to-end tests (see MedApp.E2ETests/README.md).";
            // In CI (GitHub sets CI=true) a missing credential is a broken job, not a reason to skip: a green run must
            // have run the journeys. Locally, without them, the suite is skipped.
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
                Assert.Fail(message);
            Assert.Ignore(message);
        }

        using var playwright = await Playwright.CreateAsync();
        await using var adminRequest = await SignInAsync(playwright, adminUsername, adminPassword, "Check E2E_USERNAME and E2E_PASSWORD.");
        await SaveAsync(adminRequest, E2ESettings.AdminStorageStatePath);

        try
        {
            Staff = await CreateStaffAsync(playwright, new E2EApi(adminRequest));
        }
        catch
        {
            try
            {
                await DeleteStaffAsync(new E2EApi(adminRequest));
            }
            catch (InvalidOperationException)
            {
                // The set-up's own failure is the one to report; the README says what a broken run can leave.
            }

            throw;
        }
    }

    /// <summary>
    /// The E2E clinic, the run's role and an account holding it in that team (named, for the letters it approves), which
    /// chooses its own password first, as an account an admin makes must; then its session, saved.
    /// </summary>
    private static async Task<E2EStaff> CreateStaffAsync(IPlaywright playwright, E2EApi admin)
    {
        var team = await admin.EnsureTeamAsync(E2EStaff.TeamName);
        var role = await admin.CreateRoleAsync(E2EData.UniqueName("Staff role"), E2EStaff.Permissions);
        _roleId = role.Id;

        var username = E2EData.UniqueName("staff").Replace(' ', '-').ToLowerInvariant();
        var firstPassword = await admin.CreateUserAsync(username, role.Name);
        _staffUsername = username;
        await admin.SetTeamsAsync(username, team.Id);
        const string fullName = "Dr Synthetic Clinician";
        await admin.SetStaffDetailsAsync(username, fullName, "Consultant (synthetic)");

        const string hint = "The run's member of staff couldn't sign in.";
        var password = E2EData.NewPassword();
        await using (var first = await SignInAsync(playwright, username, firstPassword, hint))
            await new E2EApi(first).UpdateOwnPasswordAsync(username, firstPassword, password);

        await using var staffRequest = await SignInAsync(playwright, username, password, hint);
        await SaveAsync(staffRequest, E2ESettings.StaffStorageStatePath);
        return new E2EStaff(username, password, await admin.UserIdOfAsync(username), fullName, team, role.Id);
    }

    /// <summary>Signs both sessions out when the run ends, and deletes the run's account and role.</summary>
    [OneTimeTearDown]
    public async Task SignOutAsync()
    {
        using var playwright = await Playwright.CreateAsync();
        if (File.Exists(E2ESettings.StaffStorageStatePath))
        {
            await using var staff = await NewContextAsync(playwright, E2ESettings.StaffStorageStatePath);
            await staff.PostAsync("/api/auth/logout");
            File.Delete(E2ESettings.StaffStorageStatePath);
        }

        if (!File.Exists(E2ESettings.AdminStorageStatePath))
            return;

        await using var admin = await NewContextAsync(playwright, E2ESettings.AdminStorageStatePath);
        try
        {
            await DeleteStaffAsync(new E2EApi(admin));
        }
        finally
        {
            await admin.PostAsync("/api/auth/logout");
            File.Delete(E2ESettings.AdminStorageStatePath);
        }
    }

    /// <summary>Deletes the run's account, then its role (once nobody holds it), whichever were made.</summary>
    private static async Task DeleteStaffAsync(E2EApi admin)
    {
        if (_staffUsername is { } username)
            await admin.DeleteUserAsync(username, E2ESettings.Password!);
        if (_roleId is { } roleId)
            await admin.DeleteRoleAsync(roleId);
        _staffUsername = null;
        _roleId = null;
    }

    private static async Task<IAPIRequestContext> SignInAsync(IPlaywright playwright, string username, string password, string hint)
    {
        var request = await NewContextAsync(playwright, null);
        var response = await request.PostAsync("/api/auth/login", new APIRequestContextOptions
        {
            DataObject = new { username, password },
        });
        if (!response.Ok)
        {
            await request.DisposeAsync();
            Assert.Fail($"Signing in to {E2ESettings.BaseUrl} failed with {response.Status}. {hint}");
        }

        return request;
    }

    private static Task<IAPIRequestContext> NewContextAsync(IPlaywright playwright, string? storageStatePath) =>
        playwright.APIRequest.NewContextAsync(new APIRequestNewContextOptions
        {
            BaseURL = E2ESettings.BaseUrl,
            IgnoreHTTPSErrors = true, // ASP.NET Core dev certificates
            StorageStatePath = storageStatePath,
        });

    private static async Task SaveAsync(IAPIRequestContext request, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await request.StorageStateAsync(new APIRequestContextStorageStateOptions { Path = path });
    }
}
