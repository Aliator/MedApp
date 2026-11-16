using FluentAssertions;
using MedApp.Infrastructure.Common.Extensions;
using MedApp.Infrastructure.Common.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace MedApp.UnitTests.Infrastructure.Common.Identity;

[TestFixture]
public sealed class DataProtectionKeyCheckTests
{
    private const string KeyUri = "https://medapp-test.vault.azure.net/keys/dataprotection";

    private static IConfiguration Configuration(string? keyUri) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [ServiceExtensions.DataProtectionKeyVaultKeySetting] = keyUri })
            .Build();

    private static async Task<int> WarningsAsync(string environmentName, string? keyUri)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.EnvironmentName).Returns(environmentName);
        var logger = new Mock<ILogger<DataProtectionKeyCheck>>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        var check = new DataProtectionKeyCheck(Configuration(keyUri), environment.Object, logger.Object);
        await check.StartAsync(CancellationToken.None);
        await check.StopAsync(CancellationToken.None);

        return logger.Invocations.Count(i => i.Method.Name == nameof(ILogger.Log) && (LogLevel)i.Arguments[0] == LogLevel.Warning);
    }

    [Test]
    public async Task Start_NotWrappedOutsideDevelopment_Warns() =>
        (await WarningsAsync(Environments.Production, null)).Should().Be(1);

    [Test]
    public async Task Start_WrappedOrInDevelopment_SaysNothing()
    {
        (await WarningsAsync(Environments.Production, KeyUri)).Should().Be(0);
        (await WarningsAsync(Environments.Development, null)).Should().Be(0);
    }

    [Test]
    public void IsWrapped_NeedsAKeyUri()
    {
        DataProtectionKeyCheck.IsWrapped(Configuration(KeyUri)).Should().BeTrue();
        DataProtectionKeyCheck.IsWrapped(Configuration(" ")).Should().BeFalse();
    }
}
