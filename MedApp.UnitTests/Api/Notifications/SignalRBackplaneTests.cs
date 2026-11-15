using FluentAssertions;
using MedApp.API.Notifications;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MedApp.UnitTests.Api.Notifications;

[TestFixture]
public sealed class SignalRBackplaneTests
{
    /// <summary>A synthetic backplane address; nothing connects to it in these tests.</summary>
    private const string BackplaneConnectionString = "medapp-valkey:6379,password=test-password";

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void AddBackplane_NoConnectionString_KeepsSignalRsOwnInProcessDelivery(string? connectionString)
    {
        var services = Services(connectionString);

        LifetimeManagerOf(services).Should().Be(typeof(DefaultHubLifetimeManager<>));
    }

    [Test]
    public void AddBackplane_ConnectionString_SendsThroughRedis()
    {
        var services = Services(BackplaneConnectionString);

        LifetimeManagerOf(services).Should().Be(typeof(RedisHubLifetimeManager<>));
    }

    [Test]
    public void AddBackplane_ConnectionString_PrefixesItsChannelsAndStartsWithoutTheServer()
    {
        using var provider = Services(BackplaneConnectionString).BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<RedisOptions>>().Value.Configuration;

        options.ChannelPrefix.ToString().Should().Be(SignalRBackplane.ChannelPrefix);
        options.AbortOnConnectFail.Should().BeFalse();
        options.EndPoints.Should().ContainSingle().Which.ToString().Should().Be("Unspecified/medapp-valkey:6379");
        options.Password.Should().Be("test-password");
    }

    private static IServiceCollection Services(string? connectionString)
    {
        var settings = new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{SignalRBackplane.ConnectionName}"] = connectionString,
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSignalR().AddBackplane(configuration);
        return services;
    }

    /// <summary>The hub lifetime manager SignalR will use: the last one registered.</summary>
    private static Type? LifetimeManagerOf(IServiceCollection services) =>
        services.Last(d => d.ServiceType == typeof(HubLifetimeManager<>)).ImplementationType;
}
