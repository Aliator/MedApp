using FluentAssertions;
using MedApp.Application.Authentication.Roles.Common;
using MedApp.Application.Authentication.Roles.Repositories;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Security;
using Moq;
using static MedApp.UnitTests.Common.Constants.RolesTestConstants;

namespace MedApp.UnitTests.Common.Fixtures;

/// <summary>
/// The role handlers' test setup: roles kept in memory (<see cref="Roles"/>), a transaction that runs the work once,
/// the security events recorded with whether they were inside it, the notices to other admins, and a fixed clock.
/// </summary>
public abstract class RoleHandlerTestBase
{
    protected FakeRoleRepository Roles = null!;
    protected PassThroughIdentityTransactions Transactions = null!;
    protected RecordingSecurityEventLog Events = null!;
    protected Mock<ISecurityNotices> Notices = null!;
    protected FixedTimeProvider Clock = null!;

    [SetUp]
    public void SetUpRoles()
    {
        Transactions = new PassThroughIdentityTransactions();
        Roles = new FakeRoleRepository();
        Events = new RecordingSecurityEventLog(Transactions);
        Notices = new Mock<ISecurityNotices>();
        Clock = new FixedTimeProvider(Now);
    }

    /// <summary>The one notice sent to the other admins: what the admin did.</summary>
    protected string NoticeSent()
    {
        var invocation = Notices.Invocations.Single(i => i.Method.Name == nameof(ISecurityNotices.SendRoleChangeToOtherAdminsAsync));
        invocation.Arguments[0].Should().Be(ActorId);
        invocation.Arguments[1].Should().Be(ActorUsername);
        return invocation.Arguments[2].Should().BeOfType<string>().Subject;
    }

    protected void NoNoticeSent() => Notices.Invocations.Should().BeEmpty();
}

/// <summary>Records security events, and whether each was recorded inside the transaction.</summary>
public sealed class RecordingSecurityEventLog(PassThroughIdentityTransactions transactions) : ISecurityEventLog
{
    public List<(SecurityEvent Event, bool InTransaction)> Recorded { get; } = [];

    public Task RecordAsync(SecurityEvent securityEvent, CancellationToken ct)
    {
        Recorded.Add((securityEvent, transactions.IsRunning));
        return Task.CompletedTask;
    }
}

/// <summary>Roles in memory, behaving as <c>RoleRepository</c> does (versions checked, names unique ignoring case).</summary>
public sealed class FakeRoleRepository : IRoleRepository
{
    public Dictionary<Guid, RoleData> Stored { get; } = [];

    public Dictionary<Guid, List<RoleHolder>> Holders { get; } = [];

    public List<CancellationToken> Tokens { get; } = [];

    public void Add(RoleData role, params RoleHolder[] holders)
    {
        Stored[role.Id] = role;
        Holders[role.Id] = [.. holders];
    }

    public Task<IReadOnlyList<RoleData>> GetAllAsync(CancellationToken ct)
    {
        Tokens.Add(ct);
        return Task.FromResult<IReadOnlyList<RoleData>>(Stored.Values.ToList());
    }

    public Task<RoleData?> GetAsync(Guid id, CancellationToken ct)
    {
        Tokens.Add(ct);
        return Task.FromResult(Stored.GetValueOrDefault(id));
    }

    public Task<IReadOnlyDictionary<Guid, int>> CountHoldersAsync(CancellationToken ct)
    {
        Tokens.Add(ct);
        return Task.FromResult<IReadOnlyDictionary<Guid, int>>(
            Holders.Where(h => h.Value.Count > 0).ToDictionary(h => h.Key, h => h.Value.Count));
    }

    public Task<IReadOnlyList<RoleHolder>> GetHoldersAsync(Guid roleId, CancellationToken ct)
    {
        Tokens.Add(ct);
        return Task.FromResult<IReadOnlyList<RoleHolder>>(Holders.GetValueOrDefault(roleId) ?? []);
    }

    public Task<bool> NameExistsAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        Tokens.Add(ct);
        return Task.FromResult(Stored.Values.Any(r =>
            string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase) && r.Id != exceptId));
    }

    public Task AddAsync(RoleData role, CancellationToken ct)
    {
        Tokens.Add(ct);
        Add(role);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(RoleData role, string expectedVersion, CancellationToken ct)
    {
        Tokens.Add(ct);
        if (!Stored.TryGetValue(role.Id, out var saved) || saved.Version != expectedVersion || saved.IsBuiltIn)
            throw new ConflictException(RoleRules.ChangedMeanwhileMessage);
        Stored[role.Id] = role;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteIfUnheldAsync(Guid roleId, CancellationToken ct)
    {
        Tokens.Add(ct);
        if (!Stored.TryGetValue(roleId, out var role) || role.IsBuiltIn || Holders.GetValueOrDefault(roleId)?.Count > 0)
            return Task.FromResult(false);
        Stored.Remove(roleId);
        return Task.FromResult(true);
    }
}
