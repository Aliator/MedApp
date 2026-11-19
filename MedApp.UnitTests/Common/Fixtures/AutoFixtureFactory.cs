using AutoFixture;
using AutoFixture.AutoMoq;

namespace MedApp.UnitTests.Common.Fixtures;

public static class AutoFixtureFactory
{
    public static IFixture Create()
    {
        var fixture = new Fixture()
            .Customize(new AutoMoqCustomization
            {
                ConfigureMembers = true
            });

        fixture.Behaviors
            .OfType<ThrowingRecursionBehavior>()
            .ToList()
            .ForEach(b => fixture.Behaviors.Remove(b));

        fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        fixture.Customize<DateOnly>(c =>
            c.FromFactory(() =>
                DateOnly.FromDateTime(
                    DateTime.UtcNow.AddYears(-30))));

        // A made-up actor acts as the system (every team's records), unless a test gives it a scope: a random one would
        // put it behind random team walls.
        fixture.Register(() => MedApp.Application.Common.Teams.TeamScope.System);

        return fixture;
    }
}