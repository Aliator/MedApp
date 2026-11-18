using FluentAssertions;
using MedApp.Application.Authentication.Roles.Commands.CreateRole;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Security;
using MedApp.UnitTests.Common.Fixtures;
using static MedApp.UnitTests.Common.Constants.RolesTestConstants;

namespace MedApp.UnitTests.Application.Auth.Roles.Commands.CreateRole;

[TestFixture]
public sealed class CreateRoleHandlerTests : RoleHandlerTestBase
{
    private CreateRoleHandler Handler() => new(Roles, Transactions, Events, Notices.Object, Clock);

    private static CreateRoleCommand Command(string name = RoleName, params string[] permissions) =>
        new(name, "  " + Description + " ", permissions, ActorId, ActorUsername);

    [Test]
    public async Task Handle_New_SavesItWithItsPermissionsInTheCatalogueOrder_WithoutUseApp()
    {
        var response = await Handler().Handle(
            Command("  " + RoleName + " ", "WorkPathways", "UseApp", "ViewPatients", "ViewPatients"), CancellationToken.None);

        var saved = Roles.Stored.Values.Should().ContainSingle().Subject;
        saved.Name.Should().Be(RoleName);
        saved.Description.Should().Be(Description);
        saved.StoredPermissions.Should().Equal("ViewPatients", "WorkPathways");
        saved.CreatedAt.Should().Be(Now);
        saved.LastUpdated.Should().Be(Now);
        saved.Version.Should().NotBeNullOrEmpty();
        response.Id.Should().Be(saved.Id);
        response.Permissions.Should().Equal("ViewPatients", "WorkPathways");
        response.HolderCount.Should().Be(0);
        response.Holders.Should().BeEmpty();
        response.CanDelete.Should().BeTrue();
    }

    [Test]
    public async Task Handle_New_RecordsRoleCreatedInsideTheTransaction_WithTheNamesOnly()
    {
        await Handler().Handle(Command(RoleName, "ViewPatients"), CancellationToken.None);

        var (recorded, inTransaction) = Events.Recorded.Should().ContainSingle().Subject;
        inTransaction.Should().BeTrue();
        recorded.Kind.Should().Be(SecurityEventKind.RoleCreated);
        recorded.UserId.Should().Be(ActorId);
        recorded.Username.Should().Be(ActorUsername);
        recorded.Details!["role"].Should().Be(RoleName);
        recorded.Details["roleId"].Should().Be(Roles.Stored.Keys.Single());
        recorded.Details["permissions"].Should().BeEquivalentTo(new[] { "ViewPatients" });
    }

    [Test]
    public async Task Handle_New_TellsTheOtherAdmins()
    {
        await Handler().Handle(Command(RoleName, "ViewPatients", "WorkPathways"), CancellationToken.None);

        NoticeSent().Should().Be($"created the role {RoleName}, with ViewPatients, WorkPathways.");
    }

    [Test]
    public async Task Handle_NoPermissions_SaysSoInTheNotice()
    {
        await Handler().Handle(Command(RoleName), CancellationToken.None);

        NoticeSent().Should().Be($"created the role {RoleName}, with no permissions beyond using the app.");
    }

    [Test]
    public async Task Handle_NameTaken_IsAConflict_AndSavesRecordsAndTellsNothing()
    {
        Roles.Add(Role() with { Name = RoleName.ToUpperInvariant() });

        var act = () => Handler().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"A role called '{RoleName}' already exists.");
        Roles.Stored.Should().ContainSingle();
        Events.Recorded.Should().BeEmpty();
        NoNoticeSent();
    }

    [Test]
    public async Task Handle_PassesTheTokenOn()
    {
        using var cts = new CancellationTokenSource();

        await Handler().Handle(Command(), cts.Token);

        Roles.Tokens.Should().OnlyContain(t => t == cts.Token);
        Transactions.LastToken.Should().Be(cts.Token);
    }
}
