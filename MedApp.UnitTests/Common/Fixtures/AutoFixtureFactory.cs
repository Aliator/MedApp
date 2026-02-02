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

        return fixture;
    }
}