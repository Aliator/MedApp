using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using MedApp.API.ServerRendering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace MedApp.UnitTests.Api.ServerRendering;

/// <summary>
/// SsrServer starting a real Node process: a small script stands in for the page server, says it's ready as the
/// real one does, and records what it was started with. Needs Node on the PATH (skipped without it).
/// </summary>
[TestFixture]
public sealed class SsrServerProcessTests
{
    private const string ApiAddress = "http://127.0.0.1:5090";
    private string _webRoot = null!;

    [OneTimeSetUp]
    public void NeedsNode()
    {
        try
        {
            using var node = Process.Start(new ProcessStartInfo("node", "--version") { RedirectStandardOutput = true, UseShellExecute = false });
            node!.WaitForExit(10_000);
            Assume.That(node.ExitCode, Is.EqualTo(0), "Node isn't installed.");
        }
        catch (Exception exception) when (exception is not IgnoreException and not InconclusiveException)
        {
            Assert.Ignore("Node isn't installed.");
        }
    }

    [SetUp]
    public void SetUp()
    {
        _webRoot = Directory.CreateTempSubdirectory("medapp-ssr-process").FullName;
        Directory.CreateDirectory(Path.Combine(_webRoot, "ssr"));
    }

    [TearDown]
    public void TearDown()
    {
        // The process may still be letting go of its files.
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                Directory.Delete(_webRoot, recursive: true);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(200);
            }
        }
    }

    private void Script(string body) => File.WriteAllText(Path.Combine(_webRoot, "ssr", "server.mjs"), body);

    private SsrServer Server(string[] addresses, SsrOptions? options = null, bool listening = true)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.WebRootPath).Returns(_webRoot);
        var features = new FeatureCollection();
        var addressFeature = new ServerAddressesFeature();
        foreach (var address in addresses) addressFeature.Addresses.Add(address);
        if (listening) features.Set<IServerAddressesFeature>(addressFeature);
        var server = new Mock<IServer>();
        server.SetupGet(s => s.Features).Returns(features);
        // The API is already listening.
        var lifetime = new Mock<IHostApplicationLifetime>();
        lifetime.SetupGet(l => l.ApplicationStarted).Returns(new CancellationToken(canceled: true));
        return new SsrServer(Options.Create(options ?? new SsrOptions()), environment.Object, server.Object, lifetime.Object, NullLogger<SsrServer>.Instance);
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) Assert.Fail("Timed out waiting.");
            await Task.Delay(50);
        }
    }

    [Test]
    public async Task ExecuteAsync_ServerStarts_IsReadyAtItsAddressWithTheApiAndKey_AndStopsWithTheApi()
    {
        Script("""
            import {writeFileSync} from 'node:fs';
            const {PORT, HOST, MEDAPP_API_URL, MEDAPP_SSR_KEY, NODE_ENV} = process.env;
            writeFileSync(new URL('./started.json', import.meta.url), JSON.stringify({PORT, HOST, MEDAPP_API_URL, MEDAPP_SSR_KEY, NODE_ENV}));
            console.log(`MedApp pages: http://${HOST}:${PORT}/`);
            console.error('a synthetic warning');
            setInterval(() => {}, 1000);
            """);
        var ssr = Server(["https://localhost:7090", ApiAddress]);

        await ssr.StartAsync(CancellationToken.None);
        await WaitFor(() => ssr.Address is not null);

        var started = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(Path.Combine(_webRoot, "ssr", "started.json")))!;
        started["HOST"].Should().Be("127.0.0.1");
        started["MEDAPP_API_URL"].Should().Be(ApiAddress);
        started["MEDAPP_SSR_KEY"].Should().Be(ssr.Key);
        started["NODE_ENV"].Should().Be("production");
        ssr.Address.Should().Be(new Uri($"http://127.0.0.1:{started["PORT"]}/"));

        await ssr.StopAsync(CancellationToken.None);

        ssr.Address.Should().BeNull();
    }

    [Test]
    public async Task ExecuteAsync_ServerExits_StaysOff()
    {
        Script("process.exit(3);");
        var ssr = Server([ApiAddress]);

        await ssr.StartAsync(CancellationToken.None);
        await ssr.ExecuteTask!;

        ssr.Address.Should().BeNull();
    }

    [Test]
    public async Task ExecuteAsync_NodeMissing_StaysOff()
    {
        Script("// never run");
        var ssr = Server([ApiAddress], new SsrOptions { NodePath = Path.Combine(_webRoot, "no-such-node") });

        await ssr.StartAsync(CancellationToken.None);
        await ssr.ExecuteTask!;

        ssr.Address.Should().BeNull();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task ExecuteAsync_ApiHasNoAddress_StaysOff(bool hasAddressFeature)
    {
        Script("// never run");
        var ssr = Server([], listening: hasAddressFeature);

        await ssr.StartAsync(CancellationToken.None);
        await ssr.ExecuteTask!;

        ssr.Address.Should().BeNull();
    }
}
