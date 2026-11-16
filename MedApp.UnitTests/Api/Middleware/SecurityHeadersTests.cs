using FluentAssertions;
using MedApp.API.Middleware;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;

namespace MedApp.UnitTests.Api.Middleware;

[TestFixture]
public sealed class SecurityHeadersTests
{
    private static async Task<HttpContext> Run(string path, string environment = "Production", bool reportOnly = false)
    {
        var host = new Mock<IWebHostEnvironment>();
        host.SetupGet(h => h.EnvironmentName).Returns(environment);
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        var middleware = new SecurityHeaders(
            _ => Task.CompletedTask,
            host.Object,
            Options.Create(new SecurityHeadersOptions { PageCspReportOnly = reportOnly }));

        await middleware.InvokeAsync(context);
        return context;
    }

    [TestCase("/api/patients")]
    [TestCase("/patients/1")]
    [TestCase("/assets/index.js")]
    public async Task Invoke_AnyResponse_GetsTheBrowserHeaders(string path)
    {
        var headers = (await Run(path)).Response.Headers;

        headers.XContentTypeOptions.ToString().Should().Be("nosniff");
        headers["Referrer-Policy"].ToString().Should().Be("strict-origin-when-cross-origin");
        headers.XFrameOptions.ToString().Should().Be("DENY");
        headers["Permissions-Policy"].ToString().Should().Contain("camera=()").And.Contain("geolocation=()").And.Contain("microphone=()");
    }

    [TestCase("/api/patients")]
    [TestCase("/API/auth/whoami")]
    [TestCase("/api/notifications/hub")]
    public async Task Invoke_Api_IsNeverStoredAndMayLoadNothing(string path)
    {
        var context = await Run(path);

        context.Response.Headers.CacheControl.ToString().Should().Be("no-store");
        context.Response.Headers.ContentSecurityPolicy.ToString().Should().Be("default-src 'none'; frame-ancestors 'none'");
        SecurityHeaders.GetNonce(context).Should().BeNull();
    }

    [Test]
    public async Task Invoke_Page_GetsTheStrictPolicyWithANewNonce()
    {
        var first = await Run("/patients");
        var second = await Run("/patients");

        var nonce = SecurityHeaders.GetNonce(first);
        nonce.Should().NotBeNullOrEmpty();
        Convert.FromBase64String(nonce!).Should().HaveCount(16);
        SecurityHeaders.GetNonce(second).Should().NotBe(nonce);

        var policy = first.Response.Headers.ContentSecurityPolicy.ToString();
        policy.Should().Be(SecurityHeaders.PagePolicy(nonce!));
        policy.Should().Contain($"script-src 'self' 'nonce-{nonce}';")
            .And.Contain("object-src 'none'")
            .And.Contain("base-uri 'none'")
            .And.Contain("frame-ancestors 'none'")
            .And.Contain("connect-src 'self'")
            .And.NotContain("unsafe-eval");
        first.Response.Headers.CacheControl.ToString().Should().BeEmpty();
    }

    [Test]
    public async Task Invoke_PageWithReportOnly_SendsThePolicyToReportOnly()
    {
        var context = await Run("/patients", reportOnly: true);

        context.Response.Headers.ContentSecurityPolicy.ToString().Should().BeEmpty();
        context.Response.Headers.ContentSecurityPolicyReportOnly.ToString()
            .Should().Be(SecurityHeaders.PagePolicy(SecurityHeaders.GetNonce(context)!));
    }

    [Test]
    public async Task Invoke_PageInDevelopment_OnlyForbidsFraming()
    {
        var context = await Run("/swagger/index.html", Environments.Development);

        context.Response.Headers.ContentSecurityPolicy.ToString().Should().Be("frame-ancestors 'none'");
        SecurityHeaders.GetNonce(context).Should().BeNull();
    }

    [Test]
    public async Task Invoke_ApiInDevelopment_StillGetsTheApiPolicy()
    {
        var context = await Run("/api/patients", Environments.Development);

        context.Response.Headers.ContentSecurityPolicy.ToString().Should().Be(SecurityHeaders.ApiPolicy);
        context.Response.Headers.CacheControl.ToString().Should().Be("no-store");
    }

    [Test]
    public async Task Invoke_CallsTheRestOfThePipeline()
    {
        var called = false;
        var host = new Mock<IWebHostEnvironment>();
        host.SetupGet(h => h.EnvironmentName).Returns(Environments.Production);
        var middleware = new SecurityHeaders(_ => { called = true; return Task.CompletedTask; }, host.Object, Options.Create(new SecurityHeadersOptions()));

        await middleware.InvokeAsync(new DefaultHttpContext());

        called.Should().BeTrue();
    }
}
