using FluentAssertions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Application.Pathways.Common;

[TestFixture]
public sealed class PathwayActorTests
{
    private static readonly Guid UserId = Guid.Parse("0000000e-0000-0000-0000-0000000000a1");

    [Test]
    public void Has_ThePermissionsGiven()
    {
        var actor = new PathwayActor(UserId, "test.clinician", StandardRoles.Clinician.Permissions);

        actor.Has(Permission.UnlockStages).Should().BeTrue();
        actor.Has(Permission.FinishWithoutVisit).Should().BeTrue();
        actor.Has(Permission.CorrectRecords).Should().BeFalse();
    }

    [Test]
    public void Has_NoPermissionsGiven_IsFalse()
    {
        new PathwayActor(UserId, "test.staff").Has(Permission.UseApp).Should().BeFalse();
    }

    [Test]
    public void Equality_ComparesThePermissionsByContent()
    {
        var actor = new PathwayActor(UserId, "test.staff", TestPermissions.Staff);
        var copy = new PathwayActor(UserId, "test.staff", new HashSet<Permission>(TestPermissions.Staff));

        copy.Should().Be(actor);
        copy.GetHashCode().Should().Be(actor.GetHashCode());
        actor.Should().NotBe(actor with { Permissions = TestPermissions.Admin });
        actor.Should().NotBe(actor with { Username = "someone.else" });
        new PathwayActor(UserId, "test.staff").Should().Be(new PathwayActor(UserId, "test.staff", new HashSet<Permission>()));
        actor.Equals(null).Should().BeFalse();
    }
}
