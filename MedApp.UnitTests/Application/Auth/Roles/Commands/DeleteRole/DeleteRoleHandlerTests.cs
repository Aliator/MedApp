using FluentAssertions;
using MedApp.Application.Authentication.Roles.Commands.DeleteRole;
using MedApp.Application.Authentication.Roles.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Security;
using MedApp.UnitTests.Common.Fixtures;
using static MedApp.UnitTests.Common.Constants.RolesTestConstants;

namespace MedApp.UnitTests.Application.Auth.Roles.Commands.DeleteRole;

[TestFixture]
public sealed class DeleteRoleHandlerTests : RoleHandlerTestBase
{
    private DeleteRoleHandler Handler() => new(Roles, Transactions, Events, Notices.Object);

    private static DeleteRoleCommand Command(Guid? roleId = null) => new(roleId ?? RoleId, ActorId, ActorUsername);

    [Test]
    public async Task Handle_NobodyHoldsIt_DeletesIt_RecordsItInsideTheTransaction_AndTellsTheOtherAdmins()
    {
        Roles.Add(Role());

        await Handler().Handle(Command(), CancellationToken.None);

        Roles.Stored.Should().BeEmpty();
        var (recorded, inTransaction) = Events.Recorded.Should().ContainSingle().Subject;
        inTransaction.Should().BeTrue();
        recorded.Kind.Should().Be(SecurityEventKind.RoleDeleted);
        recorded.Details!["role"].Should().Be(RoleName);
        recorded.Details["roleId"].Should().Be(RoleId);
        recorded.Details["permissions"].Should().BeEquivalentTo(new[] { "ViewPatients", "WorkPathways" });
        NoticeSent().Should().Be($"deleted the role {RoleName}, which nobody held.");
    }

    [Test]
    public async Task Handle_SomeoneHoldsIt_IsAConflictSayingToArchiveIt()
    {
        Roles.Add(Role(), new RoleHolder(HolderId, HolderUsername));

        var act = () => Handler().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(RoleRules.HeldMessage(RoleName, 1));
        Roles.Stored.Should().ContainKey(RoleId);
        Events.Recorded.Should().BeEmpty();
        NoNoticeSent();
    }

    [Test]
    public async Task Handle_TheBuiltInAdminRole_IsAConflict()
    {
        Roles.Add(AdminRole());

        var act = () => Handler().Handle(Command(AdminRoleId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(RoleRules.BuiltInMessage);
        Roles.Stored.Should().ContainKey(AdminRoleId);
    }

    [Test]
    public async Task Handle_UnknownRole_IsNotFound()
    {
        var act = () => Handler().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_PassesTheTokenOn()
    {
        Roles.Add(Role());
        using var cts = new CancellationTokenSource();

        await Handler().Handle(Command(), cts.Token);

        Roles.Tokens.Should().OnlyContain(t => t == cts.Token);
        Transactions.LastToken.Should().Be(cts.Token);
    }
}
