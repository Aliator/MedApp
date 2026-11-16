using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using MedApp.API.ServerRendering;
using MedApp.Contracts.Authentication.Requests;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Api;

/// <summary>
/// Pages rendered by a page server of its own (<c>Ssr:ServerUrl</c>), as the Helm chart's pages container is, through
/// the whole pipeline as Production runs it. A small server on this machine stands in for the page server, records
/// what it's sent, and answers with a page that uses the nonce it was given.
/// </summary>
[TestFixture]
public sealed class PageServerTests : IntegrationTest
{
    private WebApplication _pageServer = null!;
    private readonly ConcurrentQueue<(string Path, string Query, string? Nonce, string? Cookie, string? ForwardedHost)> _requests = new();

    [SetUp]
    public async Task StartPageServer()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        _pageServer = builder.Build();
        _pageServer.Run(async context =>
        {
            var nonce = context.Request.Headers[SsrPages.NonceHeader].ToString();
            _requests.Enqueue((
                context.Request.Path.Value!,
                context.Request.QueryString.Value ?? string.Empty,
                nonce.Length > 0 ? nonce : null,
                context.Request.Headers.Cookie.ToString(),
                context.Request.Headers["X-Forwarded-Host"].ToString()));
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync($"""<!DOCTYPE html><html><body>{RenderedMarker}<script nonce="{nonce}">window.rendered = true</script></body></html>""");
        });
        await _pageServer.StartAsync();
    }

    [TearDown]
    public async Task StopPageServer() => await _pageServer.DisposeAsync();

    private const string RenderedMarker = "rendered-by-the-page-server";

    /// <summary>The API, set to forward pages to <paramref name="serverUrl"/>, and a client of its own.</summary>
    private static (WebApplicationFactory<Program> Api, HttpClient Client) ApiWithPageServer(string serverUrl)
    {
        var api = Api.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Ssr:Enabled", "true");
            builder.UseSetting("Ssr:ServerUrl", serverUrl);
        });
        var client = api.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = MedAppApiFactory.BaseAddress,
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        return (api, client);
    }

    [Test]
    public async Task Page_PageServerOfItsOwn_RendersItWithTheResponsesNonceAsTheUser()
    {
        var (api, client) = ApiWithPageServer(_pageServer.Urls.Single());
        await using var _ = api;
        await (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(AdminUsername, AdminPassword)))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        var response = await (await client.GetAsync("/patients?lastName=synthetic")).ShouldHaveStatusAsync(HttpStatusCode.OK);

        (await response.Content.ReadAsStringAsync()).Should().Contain(RenderedMarker);
        var policy = response.Headers.GetValues("Content-Security-Policy").Single();
        var nonce = Regex.Match(policy, "'nonce-([^']+)'").Groups[1].Value;
        var request = _requests.Should().ContainSingle().Subject;
        request.Nonce.Should().NotBeNullOrEmpty().And.Be(nonce);
        request.Path.Should().Be("/patients");
        request.Query.Should().Be("?lastName=synthetic");
        // The visitor's session, for the page server's own calls to the API.
        request.Cookie.Should().Contain("=");
        request.ForwardedHost.Should().Be(MedAppApiFactory.BaseAddress.Authority);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Test]
    public async Task Page_PageServerDown_SendsTheClientRenderedApp()
    {
        var url = _pageServer.Urls.Single();
        await _pageServer.StopAsync();
        var (api, client) = ApiWithPageServer(url);
        await using var _ = api;

        var response = await (await client.GetAsync("/patients")).ShouldHaveStatusAsync(HttpStatusCode.OK);

        (await response.Content.ReadAsStringAsync()).Should().Contain(MedAppApiFactory.StubAppMarker).And.NotContain(RenderedMarker);
    }

    [Test]
    public async Task NotAPage_PageServerOfItsOwn_IsNotAsked()
    {
        var (api, client) = ApiWithPageServer(_pageServer.Urls.Single());
        await using var _ = api;

        await (await client.GetAsync("/api/no-such-endpoint")).ShouldHaveStatusAsync(HttpStatusCode.NotFound);
        await (await client.GetAsync("/ssr/server.mjs")).ShouldHaveStatusAsync(HttpStatusCode.NotFound);

        _requests.Should().BeEmpty();
    }

    /// <summary>
    /// The page server's own calls for a page's data come over http to 127.0.0.1, with no key. With an https port set
    /// (as <c>ASPNETCORE_HTTPS_PORT</c> would in <c>api.env</c>), they mustn't be redirected, or every page would fall
    /// back; a request that reached the API from anywhere else still is.
    /// </summary>
    [TestCase("127.0.0.1", false)]
    [TestCase("::1", false)]
    [TestCase("::ffff:127.0.0.1", false)]
    [TestCase("10.244.0.12", true)]
    public async Task ApiCall_HttpsPortSet_IsRedirectedOnlyIfNotReceivedOnLoopback(string localAddress, bool redirected)
    {
        var api = Api.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Ssr:Enabled", "true");
            builder.UseSetting("Ssr:ServerUrl", _pageServer.Urls.Single());
            builder.UseSetting("https_port", "443");
        });
        await using var _ = api;
        var address = IPAddress.Parse(localAddress);

        var context = await api.Server.SendAsync(request =>
        {
            request.Request.Method = HttpMethods.Get;
            request.Request.Scheme = "http";
            request.Request.Host = new HostString("127.0.0.1:8080");
            request.Request.Path = "/api/auth/whoami";
            request.Connection.LocalIpAddress = address;
            request.Connection.RemoteIpAddress = address;
        });

        if (redirected)
        {
            context.Response.StatusCode.Should().Be(StatusCodes.Status307TemporaryRedirect);
            context.Response.Headers.Location.ToString().Should().StartWith("https://127.0.0.1/api/auth/whoami");
        }
        else
        {
            context.Response.StatusCode.Should().NotBe(StatusCodes.Status307TemporaryRedirect);
            context.Response.Headers.Location.ToString().Should().BeEmpty();
        }
    }
}
