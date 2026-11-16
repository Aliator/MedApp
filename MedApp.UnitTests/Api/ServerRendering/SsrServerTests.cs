using System.Diagnostics;
using System.Net;
using FluentAssertions;
using MedApp.API.ServerRendering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using static MedApp.UnitTests.Common.Constants.SsrTestConstants;

namespace MedApp.UnitTests.Api.ServerRendering;

[TestFixture]
public sealed class SsrServerTests
{
    private string _contentRoot = null!;

    [SetUp]
    public void SetUp() => _contentRoot = Directory.CreateTempSubdirectory("medapp-ssr-tests").FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_contentRoot, recursive: true);

    private SsrServer CreateServer(SsrOptions? options = null)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.ContentRootPath).Returns(_contentRoot);
        return new SsrServer(
            Options.Create(options ?? new SsrOptions()),
            environment.Object,
            Mock.Of<IServer>(),
            Mock.Of<IHostApplicationLifetime>(),
            NullLogger<SsrServer>.Instance);
    }

    private static HttpContext Request(IPAddress from, string? key)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = from;
        if (key is not null) context.Request.Headers[SsrServer.KeyHeader] = key;
        return context;
    }

    [TestCase(new[] { "https://localhost:7038", "http://localhost:5055" }, "http://localhost:5055")]
    [TestCase(new[] { "https://localhost:7038" }, "https://localhost:7038")]
    [TestCase(new[] { "http://*:8080" }, "http://127.0.0.1:8080")]
    [TestCase(new[] { "http://+:8080" }, "http://127.0.0.1:8080")]
    [TestCase(new[] { "http://[::]:8080" }, "http://127.0.0.1:8080")]
    [TestCase(new[] { "http://0.0.0.0:8080" }, "http://127.0.0.1:8080")]
    [TestCase(new[] { "not an address", "https://localhost:7038" }, "https://localhost:7038")]
    public void ApiAddressFor_Addresses_PrefersHttpOnThisMachine(string[] addresses, string expected)
    {
        SsrServer.ApiAddressFor(addresses).Should().Be(expected);
    }

    [Test]
    public void ApiAddressFor_NoAddresses_ReturnsNull()
    {
        SsrServer.ApiAddressFor([]).Should().BeNull();
    }

    [Test]
    public void IsFromRenderer_LoopbackWithItsKey_ReturnsTrue()
    {
        var server = CreateServer();

        server.IsFromRenderer(Request(IPAddress.Loopback, server.Key)).Should().BeTrue();
        server.IsFromRenderer(Request(IPAddress.IPv6Loopback, server.Key)).Should().BeTrue();
    }

    [Test]
    public void IsFromRenderer_WrongKeyMissingKeyOrAnotherMachine_ReturnsFalse()
    {
        var server = CreateServer();

        server.IsFromRenderer(Request(IPAddress.Loopback, "not-the-key")).Should().BeFalse();
        server.IsFromRenderer(Request(IPAddress.Loopback, null)).Should().BeFalse();
        server.IsFromRenderer(Request(IPAddress.Parse("192.0.2.10"), server.Key)).Should().BeFalse();
        server.IsFromRenderer(new DefaultHttpContext()).Should().BeFalse();
    }

    /// <summary>The API's environment: what Node needs, and the API's secrets, which it mustn't get.</summary>
    private static Dictionary<string, string?> ApiEnvironment(string pathName = "PATH") => new()
    {
        [pathName] = SearchPath,
        ["TZ"] = NodeTimeZone,
        ["SystemRoot"] = WindowsRoot,
        [ConnectionStringVariable] = ConnectionString,
        [BackplaneVariable] = BackplaneConnectionString,
        [FirstAdminPasswordVariable] = FirstAdminPassword,
        // Node's own settings come from the API, never the API's environment.
        ["NODE_OPTIONS"] = ApiNodeOptions,
        ["PORT"] = "1",
    };

    private static ProcessStartInfo StartInfo(Dictionary<string, string?> environment, bool isWindows) =>
        SsrServer.StartInfo(NodePath, NodeScript, NodePort, ApiUrl, RendererKey, environment, isWindows);

    [Test]
    public void StartInfo_ApiEnvironmentHasSecrets_GivesNodeOnlyWhatItNeeds()
    {
        var start = StartInfo(ApiEnvironment(), isWindows: false);

        start.Environment.Should().BeEquivalentTo(new Dictionary<string, string?>
        {
            ["PATH"] = SearchPath,
            ["TZ"] = NodeTimeZone,
            ["PORT"] = NodePort.ToString(),
            ["HOST"] = "127.0.0.1",
            ["MEDAPP_API_URL"] = ApiUrl,
            ["MEDAPP_SSR_KEY"] = RendererKey,
            ["NODE_ENV"] = "production",
            ["NODE_OPTIONS"] = "--use-system-ca",
        });
    }

    [Test]
    public void StartInfo_OnWindows_AlsoGivesNodeWhatWindowsNeeds()
    {
        // Windows calls PATH "Path".
        var start = StartInfo(ApiEnvironment(pathName: "Path"), isWindows: true);

        start.Environment.Should().ContainKey("Path").WhoseValue.Should().Be(SearchPath);
        start.Environment.Should().ContainKey("SystemRoot").WhoseValue.Should().Be(WindowsRoot);
        start.Environment.Keys.Should().NotContain([ConnectionStringVariable, BackplaneVariable, FirstAdminPasswordVariable]);
        start.Environment["NODE_OPTIONS"].Should().Be("--use-system-ca");
    }

    [Test]
    public void StartInfo_StartsTheScriptBesideItself()
    {
        var start = StartInfo([], isWindows: false);

        start.FileName.Should().Be(NodePath);
        start.ArgumentList.Should().Equal(NodeScript);
        Path.GetFullPath(start.WorkingDirectory).Should().Be(Path.GetFullPath(NodeScriptDirectory));
        start.UseShellExecute.Should().BeFalse();
        start.RedirectStandardOutput.Should().BeTrue();
        start.RedirectStandardError.Should().BeTrue();
    }

    [Test]
    public void Key_IsRandomPerServer()
    {
        CreateServer().Key.Should().NotBe(CreateServer().Key).And.HaveLength(64);
    }

    [Test]
    public async Task ExecuteAsync_NoBuiltServer_StaysOff()
    {
        var server = CreateServer();

        await server.StartAsync(CancellationToken.None);
        await server.ExecuteTask!;

        server.Address.Should().BeNull();
    }

    private async Task BuiltServer()
    {
        Directory.CreateDirectory(Path.Combine(_contentRoot, "ssr"));
        await File.WriteAllTextAsync(Path.Combine(_contentRoot, "ssr", "server.mjs"), "// synthetic");
    }

    [Test]
    public async Task ExecuteAsync_TurnedOff_StaysOffEvenWithABuiltServer()
    {
        await BuiltServer();
        var server = CreateServer(new SsrOptions { Enabled = false });

        await server.StartAsync(CancellationToken.None);
        await server.ExecuteTask!;

        server.Address.Should().BeNull();
    }

    [Test]
    public async Task ExecuteAsync_ServerInWebRootOnly_StaysOff()
    {
        // The old layout (wwwroot/ssr/server.mjs): the script is looked for in the content root, outside wwwroot.
        Directory.CreateDirectory(Path.Combine(_contentRoot, "wwwroot", "ssr"));
        await File.WriteAllTextAsync(Path.Combine(_contentRoot, "wwwroot", "ssr", "server.mjs"), "// synthetic");
        var server = CreateServer();

        await server.StartAsync(CancellationToken.None);
        await server.ExecuteTask!;

        server.Address.Should().BeNull();
    }

    [Test]
    public async Task ExecuteAsync_PageServerOfItsOwn_UsesItsAddressWithoutStartingNode()
    {
        // A built server too, which the API would otherwise start. Starting it would wait for the API to be
        // listening, which this test's API never is, so ExecuteTask finishing shows nothing was started.
        await BuiltServer();
        var server = CreateServer(new SsrOptions { ServerUrl = PageServerUrl });

        await server.StartAsync(CancellationToken.None);
        await server.ExecuteTask!;

        server.Address.Should().Be(new Uri(PageServerAddress));
    }

    [Test]
    public async Task ExecuteAsync_PageServerOfItsOwn_ItsCallsAreNotTheRenderers()
    {
        // It's given no key, so its calls to the API are ordinary requests, even from loopback.
        var server = CreateServer(new SsrOptions { ServerUrl = PageServerUrl });

        await server.StartAsync(CancellationToken.None);
        await server.ExecuteTask!;

        server.IsFromRenderer(Request(IPAddress.Loopback, null)).Should().BeFalse();
        server.IsFromRenderer(Request(IPAddress.Loopback, RendererKey)).Should().BeFalse();
    }

    [Test]
    public async Task ExecuteAsync_PageServerOfItsOwnButTurnedOff_StaysOff()
    {
        var server = CreateServer(new SsrOptions { ServerUrl = PageServerUrl, Enabled = false });

        await server.StartAsync(CancellationToken.None);
        await server.ExecuteTask!;

        server.Address.Should().BeNull();
    }

    [Test]
    public async Task ExecuteAsync_NoContentRoot_StaysOff()
    {
        var environment = new Mock<IWebHostEnvironment>();
        var server = new SsrServer(
            Options.Create(new SsrOptions()),
            environment.Object,
            Mock.Of<IServer>(),
            Mock.Of<IHostApplicationLifetime>(),
            NullLogger<SsrServer>.Instance);

        await server.StartAsync(CancellationToken.None);
        await server.ExecuteTask!;

        server.Address.Should().BeNull();
    }
}
