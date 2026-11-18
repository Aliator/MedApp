using FluentAssertions;
using MedApp.Application.Common.Identity;
using MedApp.Infrastructure.Common.Identity;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Infrastructure;

/// <summary>
/// <c>IdentityTransactions</c> against the real database (unit tests can't cover it): Identity's managers save as
/// they go, so only a real transaction can undo the saves before a failure.
/// </summary>
[TestFixture]
public sealed class IdentityTransactionsTests : IntegrationTest
{
    // Starts with no accounts, and only the standard roles the migration makes (left out of RoleNamesAsync).
    protected override bool MakesFirstAdmin => false;

    private const string RoleA = "IntegrationRoleA";
    private const string RoleB = "IntegrationRoleB";

    [Test]
    public async Task RunAsync_WorkSucceeds_CommitsEverySave()
    {
        var result = await RunInScopeAsync(services =>
        {
            var roles = services.GetRequiredService<RoleManager<ApplicationRole>>();
            return services.GetRequiredService<IIdentityTransactions>().RunAsync(async () =>
            {
                await roles.CreateAsync(new ApplicationRole(RoleA));
                return await roles.CreateAsync(new ApplicationRole(RoleB));
            }, CancellationToken.None);
        });

        result.Succeeded.Should().BeTrue();
        (await RoleNamesAsync()).Should().BeEquivalentTo([RoleA, RoleB]);
    }

    [Test]
    public async Task RunAsync_WorkFailsAfterASave_RollsTheSaveBack()
    {
        var failure = new IdentityError { Code = "Synthetic", Description = "A synthetic failure." };

        var result = await RunInScopeAsync(services =>
        {
            var roles = services.GetRequiredService<RoleManager<ApplicationRole>>();
            return services.GetRequiredService<IIdentityTransactions>().RunAsync(async () =>
            {
                (await roles.CreateAsync(new ApplicationRole(RoleA))).Succeeded.Should().BeTrue();
                return IdentityResult.Failed(failure);
            }, CancellationToken.None);
        });

        result.Errors.Should().ContainSingle(e => e.Code == failure.Code);
        (await RoleNamesAsync()).Should().BeEmpty();
    }

    [Test]
    public async Task RunAsync_WorkThrowsAfterASave_RollsTheSaveBackAndRethrows()
    {
        var act = () => RunInScopeAsync(services =>
        {
            var roles = services.GetRequiredService<RoleManager<ApplicationRole>>();
            return services.GetRequiredService<IIdentityTransactions>().RunAsync(async () =>
            {
                await roles.CreateAsync(new ApplicationRole(RoleA));
                throw new InvalidOperationException("Synthetic failure.");
            }, CancellationToken.None);
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await RoleNamesAsync()).Should().BeEmpty();
    }

    [Test]
    public async Task RunAsync_InsideAnother_JoinsItAndCommitsWithIt()
    {
        var result = await RunInScopeAsync(services =>
        {
            var roles = services.GetRequiredService<RoleManager<ApplicationRole>>();
            var transactions = services.GetRequiredService<IIdentityTransactions>();
            return transactions.RunAsync(async () =>
            {
                (await transactions.RunAsync(() => roles.CreateAsync(new ApplicationRole(RoleA)), CancellationToken.None))
                    .Succeeded.Should().BeTrue();
                return await roles.CreateAsync(new ApplicationRole(RoleB));
            }, CancellationToken.None);
        });

        result.Succeeded.Should().BeTrue();
        (await RoleNamesAsync()).Should().BeEquivalentTo([RoleA, RoleB]);
    }

    [Test]
    public async Task RunAsync_InsideAnotherThatFailsLater_IsRolledBackWithIt()
    {
        // As SetUserRolesHandler: the role service's own transaction succeeds inside the handler's, then the handler's
        // next step fails, so the roles the inner part saved must go too.
        var act = () => RunInScopeAsync(services =>
        {
            var roles = services.GetRequiredService<RoleManager<ApplicationRole>>();
            var transactions = services.GetRequiredService<IIdentityTransactions>();
            return transactions.RunAsync(async () =>
            {
                await transactions.RunAsync(() => roles.CreateAsync(new ApplicationRole(RoleA)), CancellationToken.None);
                throw new InvalidOperationException("Synthetic failure after the inner part.");
            }, CancellationToken.None);
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await RoleNamesAsync()).Should().BeEmpty();
    }

    [Test]
    public async Task FirstAdminSetup_PasswordRefused_LeavesNoRolesBehind()
    {
        // On a database without the Admin role, it's made first, in the same transaction, so a refused password must
        // undo it too.
        await RemoveEveryRoleAsync();
        var result = await RunInScopeAsync(services =>
            services.GetRequiredService<FirstAdminSetup>().RunAsync(AdminUsername, "short", CancellationToken.None));

        result.Outcome.Should().Be(FirstAdminOutcome.Failed);
        (await AllRoleNamesAsync()).Should().BeEmpty();
        (await UserCountAsync()).Should().Be(0);
    }

    [Test]
    public async Task FirstAdminSetup_NoAdminRole_MakesTheBuiltInOneAndAnAdminWithIt()
    {
        await RemoveEveryRoleAsync();
        var result = await RunInScopeAsync(services =>
            services.GetRequiredService<FirstAdminSetup>().RunAsync(AdminUsername, AdminPassword, CancellationToken.None));

        result.Outcome.Should().Be(FirstAdminOutcome.Created);
        (await AllRoleNamesAsync()).Should().BeEquivalentTo([AdminRole]);
        var client = await SignInAsAdminAsync();
        (await client.GetAsync("/api/auth/users/search")).IsSuccessStatusCode.Should().BeTrue();
    }

    [Test]
    public async Task FirstAdminSetup_AccountsExist_ChangesNothing()
    {
        await RunInScopeAsync(services =>
            services.GetRequiredService<FirstAdminSetup>().RunAsync(AdminUsername, AdminPassword, CancellationToken.None));

        var again = await RunInScopeAsync(services =>
            services.GetRequiredService<FirstAdminSetup>().RunAsync("it-second-admin", AdminPassword, CancellationToken.None));

        again.Outcome.Should().Be(FirstAdminOutcome.AccountsExist);
        (await UserCountAsync()).Should().Be(1);
    }

    /// <summary>The roles a test made: every role but the standard ones.</summary>
    private static async Task<List<string?>> RoleNamesAsync()
    {
        var standard = StandardRoles.All.Select(r => r.Name).ToList();
        return (await AllRoleNamesAsync()).Where(name => !standard.Contains(name!)).ToList();
    }

    private static Task<List<string?>> AllRoleNamesAsync() =>
        RunInScopeAsync(services =>
            services.GetRequiredService<RoleManager<ApplicationRole>>().Roles.Select(r => r.Name).ToListAsync());

    /// <summary>A database from before roles had permissions, without even the Admin role.</summary>
    private static Task RemoveEveryRoleAsync() => ExecuteSqlAsync($"DELETE FROM AspNetRoles");

    private static Task<int> UserCountAsync() =>
        RunInScopeAsync(services => services.GetRequiredService<UserManager<ApplicationUser>>().Users.CountAsync());
}
