using FluentAssertions;
using MedApp.Application.Common.Authentication;
using MedApp.Infrastructure.Common.Identity;
using MedApp.Infrastructure.Data;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Auth;

/// <summary>
/// <c>SessionService</c> against the real database (unit tests can't cover it): each call in its own scope, as each
/// request has its own.
/// </summary>
[TestFixture]
public sealed class SessionServiceTests : IntegrationTest
{
    private const string IpAddress = "203.0.113.7"; // TEST-NET-3: never a real address
    private const string UserAgent = "MedApp integration tests";

    private Guid _adminId;

    [SetUp]
    public async Task FindAdminAsync()
    {
        _adminId = await RunInScopeAsync(async services =>
            (await services.GetRequiredService<UserManager<ApplicationUser>>().FindByNameAsync(AdminUsername))!.Id);
    }

    [Test]
    public async Task CreateSessionAsync_User_StoresAnActiveSessionForEightHours()
    {
        var before = DateTime.UtcNow;

        var login = await Sessions(s => s.CreateSessionAsync(_adminId, IpAddress, UserAgent));

        var stored = await RunInScopeAsync(services =>
            services.GetRequiredService<MedAppDbContext>().UserSessions.AsNoTracking().SingleAsync(s => s.Id == login.SessionId));
        stored.UserId.Should().Be(_adminId);
        stored.CreatedByIp.Should().Be(IpAddress);
        stored.UserAgent.Should().Be(UserAgent);
        stored.RevokedAtUtc.Should().BeNull();
        stored.CreatedAtUtc.Should().BeOnOrAfter(before.AddSeconds(-1));
        (stored.ExpiresAtUtc - stored.CreatedAtUtc).Should().Be(TimeSpan.FromHours(8));
        login.ExpiresAtUtc.Should().BeCloseTo(stored.ExpiresAtUtc, TimeSpan.FromMilliseconds(1));
    }

    [Test]
    public async Task ValidateSessionAsync_ActiveSession_ReturnsTheUserAndRoles()
    {
        var login = await Sessions(s => s.CreateSessionAsync(_adminId, null, null));

        var result = await Sessions(s => s.ValidateSessionAsync(login.SessionId));

        result.Should().NotBeNull();
        result!.UserId.Should().Be(_adminId);
        result.Username.Should().Be(AdminUsername);
        result.Roles.Should().BeEquivalentTo([AdminRole]);
        result.Permissions.Should().BeEquivalentTo(MedApp.Application.Common.Identity.Permissions.Admin);
    }

    [Test]
    public async Task ValidateSessionAsync_UnknownSession_ReturnsNull()
    {
        (await Sessions(s => s.ValidateSessionAsync(Guid.NewGuid()))).Should().BeNull();
    }

    [Test]
    public async Task ValidateSessionAsync_RevokedSession_ReturnsNull()
    {
        var login = await Sessions(s => s.CreateSessionAsync(_adminId, null, null));
        await RunInScopeAsync(services => services.GetRequiredService<ISessionService>().RevokeSessionAsync(login.SessionId));

        (await Sessions(s => s.ValidateSessionAsync(login.SessionId))).Should().BeNull();
    }

    [Test]
    public async Task ValidateSessionAsync_ExpiredSession_ReturnsNull()
    {
        var login = await Sessions(s => s.CreateSessionAsync(_adminId, null, null));
        await ExecuteSqlAsync($"UPDATE UserSessions SET ExpiresAtUtc = {DateTime.UtcNow.AddMinutes(-5)} WHERE Id = {login.SessionId}");

        (await Sessions(s => s.ValidateSessionAsync(login.SessionId))).Should().BeNull();
    }

    [Test]
    public async Task ValidateSessionAsync_UserDeleted_ReturnsNull()
    {
        var staff = await CreateStaffAsync(UserRole);
        var login = await Sessions(s => s.CreateSessionAsync(staff.UserId, null, null));
        await RunInScopeAsync(async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            await users.DeleteAsync((await users.FindByIdAsync(staff.UserId.ToString()))!);
        });

        (await Sessions(s => s.ValidateSessionAsync(login.SessionId))).Should().BeNull();
    }

    [Test]
    public async Task RevokeSessionAsync_RevokedTwice_KeepsTheFirstTime()
    {
        var login = await Sessions(s => s.CreateSessionAsync(_adminId, null, null));
        await RunInScopeAsync(services => services.GetRequiredService<ISessionService>().RevokeSessionAsync(login.SessionId));
        var first = await RevokedAtAsync(login.SessionId);

        await RunInScopeAsync(services => services.GetRequiredService<ISessionService>().RevokeSessionAsync(login.SessionId));

        first.Should().NotBeNull();
        (await RevokedAtAsync(login.SessionId)).Should().Be(first);
    }

    [Test]
    public async Task RevokeSessionAsync_UnknownSession_DoesNothing()
    {
        var act = () => RunInScopeAsync(services => services.GetRequiredService<ISessionService>().RevokeSessionAsync(Guid.NewGuid()));

        await act.Should().NotThrowAsync();
    }

    private static Task<T> Sessions<T>(Func<ISessionService, Task<T>> call) =>
        RunInScopeAsync(services => call(services.GetRequiredService<ISessionService>()));

    private static Task<DateTime?> RevokedAtAsync(Guid sessionId) =>
        RunInScopeAsync(async services =>
            (await services.GetRequiredService<MedAppDbContext>().UserSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId))
            .RevokedAtUtc);
}
