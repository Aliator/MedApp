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
    // Starts with an empty database: no roles and no accounts.
    protected override bool MakesFirstAdmin => false;

    private const string RoleA = "IntegrationRoleA";
    private const string RoleB = "IntegrationRoleB";

    [Test]
    public async Task RunAsync_WorkSucceeds_CommitsEverySave()
    {
        var result = await RunInScopeAsync(services =>
        {
            var roles = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            return services.GetRequiredService<IIdentityTransactions>().RunAsync(async () =>
            {
                await roles.CreateAsync(new IdentityRole<Guid>(RoleA));
                return await roles.CreateAsync(new IdentityRole<Guid>(RoleB));
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
            var roles = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            return services.GetRequiredService<IIdentityTransactions>().RunAsync(async () =>
            {
                (await roles.CreateAsync(new IdentityRole<Guid>(RoleA))).Succeeded.Should().BeTrue();
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
            var roles = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            return services.GetRequiredService<IIdentityTransactions>().RunAsync(async () =>
            {
                await roles.CreateAsync(new IdentityRole<Guid>(RoleA));
                throw new InvalidOperationException("Synthetic failure.");
            }, CancellationToken.None);
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await RoleNamesAsync()).Should().BeEmpty();
    }

    [Test]
    public async Task FirstAdminSetup_PasswordRefused_LeavesNoRolesBehind()
    {
        // The roles are made first, in the same transaction, so a refused password must undo them too.
        var result = await RunInScopeAsync(services =>
            services.GetRequiredService<FirstAdminSetup>().RunAsync(AdminUsername, "short", CancellationToken.None));

        result.Outcome.Should().Be(FirstAdminOutcome.Failed);
        (await RoleNamesAsync()).Should().BeEmpty();
        (await UserCountAsync()).Should().Be(0);
    }

    [Test]
    public async Task FirstAdminSetup_NewDatabase_MakesTheRolesAndAnAdminWithBoth()
    {
        var result = await RunInScopeAsync(services =>
            services.GetRequiredService<FirstAdminSetup>().RunAsync(AdminUsername, AdminPassword, CancellationToken.None));

        result.Outcome.Should().Be(FirstAdminOutcome.Created);
        (await RoleNamesAsync()).Should().BeEquivalentTo([AdminRole, UserRole]);
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

    private static Task<List<string?>> RoleNamesAsync() =>
        RunInScopeAsync(services =>
            services.GetRequiredService<RoleManager<IdentityRole<Guid>>>().Roles.Select(r => r.Name).ToListAsync());

    private static Task<int> UserCountAsync() =>
        RunInScopeAsync(services => services.GetRequiredService<UserManager<ApplicationUser>>().Users.CountAsync());
}
