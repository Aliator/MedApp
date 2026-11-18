using FluentAssertions;
using MedApp.Application.Authentication.Roles.Commands.UpdateRole;
using MedApp.Application.Authentication.Roles.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Security;
using MedApp.UnitTests.Common.Fixtures;
using static MedApp.UnitTests.Common.Constants.RolesTestConstants;

namespace MedApp.UnitTests.Application.Auth.Roles.Commands.UpdateRole;

[TestFixture]
public sealed class UpdateRoleHandlerTests : RoleHandlerTestBase
{
    private UpdateRoleHandler Handler() => new(Roles, Transactions, Events, Notices.Object, Clock);

    private static UpdateRoleCommand Command(
        string name = RoleName, string? description = Description, string version = RoleVersion, Guid? roleId = null, params string[] permissions) =>
        new(roleId ?? RoleId, name, description, permissions, version, ActorId, ActorUsername);

    [SetUp]
    public void SetUp() => Roles.Add(Role(), new RoleHolder(HolderId, HolderUsername));

    [Test]
    public async Task Handle_PermissionsChanged_SavesThemWithANewVersion_AndReturnsTheRoleWithItsHolders()
    {
        var response = await Handler().Handle(
            Command(permissions: ["ViewPatients", "AssignPathways"]), CancellationToken.None);

        var saved = Roles.Stored[RoleId];
        saved.StoredPermissions.Should().Equal("ViewPatients", "AssignPathways");
        saved.Version.Should().NotBe(RoleVersion);
        saved.LastUpdated.Should().Be(Now);
        saved.CreatedAt.Should().Be(CreatedAt);
        response.Version.Should().Be(saved.Version);
        response.Permissions.Should().Equal("ViewPatients", "AssignPathways");
        response.HolderCount.Should().Be(1);
        response.Holders!.Single().Username.Should().Be(HolderUsername);
    }

    [Test]
    public async Task Handle_Changed_RecordsRoleChangedInsideTheTransaction_WithThePermissionNamesAddedAndRemoved()
    {
        await Handler().Handle(Command(OtherRoleName, "New words.", permissions: ["ViewPatients", "AssignPathways"]), CancellationToken.None);

        var (recorded, inTransaction) = Events.Recorded.Should().ContainSingle().Subject;
        inTransaction.Should().BeTrue();
        recorded.Kind.Should().Be(SecurityEventKind.RoleChanged);
        recorded.UserId.Should().Be(ActorId);
        recorded.Details.Should().BeEquivalentTo(new Dictionary<string, object?>
        {
            ["roleId"] = RoleId,
            ["role"] = OtherRoleName,
            ["renamedFrom"] = RoleName,
            ["descriptionChanged"] = true,
            ["added"] = new List<string> { "AssignPathways" },
            ["removed"] = new List<string> { "WorkPathways" },
        });
    }

    [Test]
    public async Task Handle_Changed_TellsTheOtherAdmins()
    {
        await Handler().Handle(Command(permissions: ["ViewPatients", "AssignPathways"]), CancellationToken.None);

        NoticeSent().Should().Be($"changed the role {RoleName}: added AssignPathways; removed WorkPathways.");
    }

    [Test]
    public async Task Handle_NothingChanged_SavesRecordsAndTellsNothing()
    {
        var response = await Handler().Handle(
            Command(RoleName, Description + "  ", permissions: ["WorkPathways", "ViewPatients", "UseApp"]), CancellationToken.None);

        Roles.Stored[RoleId].Version.Should().Be(RoleVersion);
        Events.Recorded.Should().BeEmpty();
        NoNoticeSent();
        response.Version.Should().Be(RoleVersion);
    }

    [Test]
    public async Task Handle_TheBuiltInAdminRole_IsAConflict()
    {
        Roles.Add(AdminRole());

        var act = () => Handler().Handle(Command(roleId: AdminRoleId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(RoleRules.BuiltInMessage);
        Events.Recorded.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_FromAnOlderVersion_IsAConflict()
    {
        var act = () => Handler().Handle(Command(version: "older", permissions: ["ViewPatients"]), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(RoleRules.ChangedMeanwhileMessage);
        Roles.Stored[RoleId].StoredPermissions.Should().Equal("ViewPatients", "WorkPathways");
        Events.Recorded.Should().BeEmpty();
        NoNoticeSent();
    }

    [Test]
    public async Task Handle_AnotherAdminSavedAtTheSameMoment_IsAConflict_AndRecordsNothing()
    {
        // Read at RoleVersion, but someone else's save lands before this one's.
        Roles.Stored[RoleId] = Roles.Stored[RoleId] with { Version = RoleVersion };
        var repository = new SavedMeanwhile(Roles);
        var handler = new UpdateRoleHandler(repository, Transactions, Events, Notices.Object, Clock);

        var act = () => handler.Handle(Command(permissions: ["ViewPatients"]), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(RoleRules.ChangedMeanwhileMessage);
        Events.Recorded.Should().BeEmpty();
        NoNoticeSent();
    }

    [Test]
    public async Task Handle_RenamedToATakenName_IsAConflict()
    {
        Roles.Add(Role() with { Id = Guid.NewGuid(), Name = OtherRoleName });

        var act = () => Handler().Handle(Command(OtherRoleName.ToLowerInvariant(), permissions: ["ViewPatients", "WorkPathways"]), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"A role called '{OtherRoleName.ToLowerInvariant()}' already exists.");
    }

    [Test]
    public async Task Handle_UnknownRole_IsNotFound()
    {
        var act = () => Handler().Handle(Command(roleId: Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_AStoredNameCodeDoesNotKnow_IsDroppedOnSave_WithoutBeingReportedAsRemoved()
    {
        Roles.Stored[RoleId] = Role("ViewPatients", "RetiredPermission");

        await Handler().Handle(Command(permissions: ["ViewPatients", "WorkPathways"]), CancellationToken.None);

        Roles.Stored[RoleId].StoredPermissions.Should().Equal("ViewPatients", "WorkPathways");
        var details = Events.Recorded.Single().Event.Details!;
        details["added"].Should().BeEquivalentTo(new[] { "WorkPathways" });
        details.Should().NotContainKey("removed");
    }

    [Test]
    public async Task Handle_PassesTheTokenOn()
    {
        using var cts = new CancellationTokenSource();

        await Handler().Handle(Command(permissions: ["ViewPatients"]), cts.Token);

        Roles.Tokens.Should().OnlyContain(t => t == cts.Token);
        Transactions.LastToken.Should().Be(cts.Token);
    }

    /// <summary>A repository where another admin's save lands between this change's read and its save.</summary>
    private sealed class SavedMeanwhile(FakeRoleRepository inner) : global::MedApp.Application.Authentication.Roles.Repositories.IRoleRepository
    {
        public Task<IReadOnlyList<RoleData>> GetAllAsync(CancellationToken ct) => inner.GetAllAsync(ct);
        public Task<RoleData?> GetAsync(Guid id, CancellationToken ct) => inner.GetAsync(id, ct);
        public Task<IReadOnlyDictionary<Guid, int>> CountHoldersAsync(CancellationToken ct) => inner.CountHoldersAsync(ct);
        public Task<IReadOnlyList<RoleHolder>> GetHoldersAsync(Guid roleId, CancellationToken ct) => inner.GetHoldersAsync(roleId, ct);
        public Task<bool> NameExistsAsync(string name, Guid? exceptId, CancellationToken ct) => inner.NameExistsAsync(name, exceptId, ct);
        public Task AddAsync(RoleData role, CancellationToken ct) => inner.AddAsync(role, ct);
        public Task<bool> DeleteIfUnheldAsync(Guid roleId, CancellationToken ct) => inner.DeleteIfUnheldAsync(roleId, ct);

        public Task UpdateAsync(RoleData role, string expectedVersion, CancellationToken ct)
        {
            inner.Stored[role.Id] = inner.Stored[role.Id] with { Version = "someone-else's" };
            return inner.UpdateAsync(role, expectedVersion, ct);
        }
    }
}
