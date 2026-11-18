using FluentAssertions;
using MedApp.Application.Authentication.Roles.Commands.SetRoleArchived;
using MedApp.Application.Authentication.Roles.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Security;
using MedApp.UnitTests.Common.Fixtures;
using static MedApp.UnitTests.Common.Constants.RolesTestConstants;

namespace MedApp.UnitTests.Application.Auth.Roles.Commands.SetRoleArchived;

[TestFixture]
public sealed class SetRoleArchivedHandlerTests : RoleHandlerTestBase
{
    private SetRoleArchivedHandler Handler() => new(Roles, Transactions, Events, Notices.Object, Clock);

    private static SetRoleArchivedCommand Command(bool archived, string version = RoleVersion, Guid? roleId = null) =>
        new(roleId ?? RoleId, archived, version, ActorId, ActorUsername);

    [SetUp]
    public void SetUp() => Roles.Add(Role(), new RoleHolder(HolderId, HolderUsername));

    [Test]
    public async Task Handle_Archive_ArchivesItKeepingItsPermissionsAndHolders_AndRecordsAndTellsIt()
    {
        var response = await Handler().Handle(Command(true), CancellationToken.None);

        var saved = Roles.Stored[RoleId];
        saved.IsArchived.Should().BeTrue();
        saved.StoredPermissions.Should().Equal("ViewPatients", "WorkPathways");
        saved.Version.Should().NotBe(RoleVersion);
        saved.LastUpdated.Should().Be(Now);
        response.IsArchived.Should().BeTrue();
        response.HolderCount.Should().Be(1);

        var (recorded, inTransaction) = Events.Recorded.Should().ContainSingle().Subject;
        inTransaction.Should().BeTrue();
        recorded.Kind.Should().Be(SecurityEventKind.RoleChanged);
        recorded.Details!["archived"].Should().Be(true);
        NoticeSent().Should().Be($"changed the role {RoleName}: archived it.");
    }

    [Test]
    public async Task Handle_Restore_RestoresIt()
    {
        Roles.Stored[RoleId] = Role() with { IsArchived = true };

        await Handler().Handle(Command(false), CancellationToken.None);

        Roles.Stored[RoleId].IsArchived.Should().BeFalse();
        Events.Recorded.Single().Event.Details!["archived"].Should().Be(false);
        NoticeSent().Should().Be($"changed the role {RoleName}: restored it.");
    }

    [Test]
    public async Task Handle_AlreadySo_ChangesRecordsAndTellsNothing()
    {
        var response = await Handler().Handle(Command(false), CancellationToken.None);

        Roles.Stored[RoleId].Version.Should().Be(RoleVersion);
        response.IsArchived.Should().BeFalse();
        Events.Recorded.Should().BeEmpty();
        NoNoticeSent();
    }

    [Test]
    public async Task Handle_TheBuiltInAdminRole_IsAConflict()
    {
        Roles.Add(AdminRole());

        var act = () => Handler().Handle(Command(true, roleId: AdminRoleId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(RoleRules.BuiltInMessage);
    }

    [Test]
    public async Task Handle_FromAnOlderVersion_IsAConflict()
    {
        var act = () => Handler().Handle(Command(true, "older"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(RoleRules.ChangedMeanwhileMessage);
        Roles.Stored[RoleId].IsArchived.Should().BeFalse();
    }

    [Test]
    public async Task Handle_UnknownRole_IsNotFound()
    {
        var act = () => Handler().Handle(Command(true, roleId: Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_PassesTheTokenOn()
    {
        using var cts = new CancellationTokenSource();

        await Handler().Handle(Command(true), cts.Token);

        Roles.Tokens.Should().OnlyContain(t => t == cts.Token);
        Transactions.LastToken.Should().Be(cts.Token);
    }
}
