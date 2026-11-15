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

namespace MedApp.UnitTests.Api.ServerRendering;

[TestFixture]
public sealed class SsrServerTests
{
    private string _webRoot = null!;

    [SetUp]
    public void SetUp() => _webRoot = Directory.CreateTempSubdirectory("medapp-ssr-tests").FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_webRoot, recursive: true);

    private SsrServer CreateServer(SsrOptions? options = null, string? webRoot = null)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.WebRootPath).Returns(webRoot ?? _webRoot);
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

    [Test]
    public async Task ExecuteAsync_TurnedOff_StaysOffEvenWithABuiltServer()
    {
        Directory.CreateDirectory(Path.Combine(_webRoot, "ssr"));
        await File.WriteAllTextAsync(Path.Combine(_webRoot, "ssr", "server.mjs"), "// synthetic");
        var server = CreateServer(new SsrOptions { Enabled = false });

        await server.StartAsync(CancellationToken.None);
        await server.ExecuteTask!;

        server.Address.Should().BeNull();
    }

    [Test]
    public async Task ExecuteAsync_NoWebRoot_StaysOff()
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
