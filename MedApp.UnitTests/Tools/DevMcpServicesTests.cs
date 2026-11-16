using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Notifications.Common;
using MedApp.Contracts.Notifications.Responses;
using MedApp.DevMcp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.UnitTests.Tools;

/// <summary>
/// The dev MCP tool (medapp-dev) checks its services when it starts, so a service the app's layers need but the tool
/// doesn't register stops it before it serves anything. Building them here, with the same validation, catches that.
/// </summary>
[TestFixture]
public sealed class DevMcpServicesTests
{
    private static IConfiguration Configuration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    @"Server=(localdb)\MSSQLLocalDB;Database=MedApp_DevMcpServicesTests;Trusted_Connection=True;TrustServerCertificate=True",
                ["Identity:PasswordRequiredLength"] = "8",
                ["Identity:RequireDigit"] = "true",
                ["Identity:RequireLowercase"] = "true",
                ["Identity:RequireUppercase"] = "true",
                ["Identity:RequireNonAlphanumeric"] = "false",
                ["Identity:MaxFailedAccessAttempts"] = "5",
                ["Identity:LockoutMinutes"] = "5",
            })
            .Build();

    private static ServiceProvider Build() =>
        new ServiceCollection()
            .AddLogging()
            .AddDevMcpCore(Configuration())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

    [Test]
    public void AddDevMcpCore_EveryServiceResolves_WithValidationOn()
    {
        var build = () => Build().Dispose();

        build.Should().NotThrow("every handler the tool's commands use must be constructible");
    }

    [Test]
    public void AddDevMcpCore_TheActivityLogsActorIsTheTool_WithNoUserOrAddress()
    {
        using var services = Build();

        var actor = services.GetRequiredService<IActivityContext>().Actor;

        actor.Should().Be(new ActivityActor(null, DevToolActivityContext.Username, null));
    }

    [Test]
    public async Task AddDevMcpCore_LiveNotifications_PushNothing()
    {
        using var services = Build();
        var publisher = services.GetRequiredService<INotificationPublisher>();

        publisher.Should().BeOfType<NoLiveNotifications>();
        await publisher.Invoking(p => p.PublishAsync(Guid.NewGuid(),
                new NotificationResponse(Guid.NewGuid(), "ProcedureAssigned", "Title", "Message", null, DateTime.UtcNow, false),
                CancellationToken.None))
            .Should().NotThrowAsync();
    }
}
