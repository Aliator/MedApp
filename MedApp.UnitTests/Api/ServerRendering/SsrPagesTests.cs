using System.Net;
using System.Text;
using FluentAssertions;
using MedApp.API.ServerRendering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using static MedApp.UnitTests.Common.Constants.SsrTestConstants;

namespace MedApp.UnitTests.Api.ServerRendering;

[TestFixture]
public sealed class SsrPagesTests
{
    private string _webRoot = null!;
    private Mock<ISsrServer> _ssr = null!;
    private RecordingHandler _renderer = null!;

    [SetUp]
    public void SetUp()
    {
        _webRoot = Directory.CreateTempSubdirectory("medapp-ssr-pages").FullName;
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), IndexHtml);
        _ssr = new Mock<ISsrServer>();
        _ssr.SetupGet(s => s.Address).Returns(RendererAddress);
        _renderer = new RecordingHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(RenderedHtml, Encoding.UTF8, "text/html"),
            };
            response.Headers.TryAddWithoutValidation("Connection", "keep-alive");
            response.Headers.TryAddWithoutValidation("Vary", "Origin");
            return response;
        });
    }

    [TearDown]
    public void TearDown()
    {
        _renderer.Dispose();
        Directory.Delete(_webRoot, recursive: true);
    }

    private DefaultHttpContext Page(string method = "GET")
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.WebRootFileProvider).Returns(new PhysicalFileProvider(_webRoot));
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddSingleton(environment.Object).BuildServiceProvider(),
        };
        context.Request.Method = method;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("medapp.test");
        context.Request.Path = PagePath;
        context.Request.QueryString = new QueryString(PageQuery);
        context.Request.Headers.Cookie = SessionCookie;
        context.Request.Headers.AcceptLanguage = "en-GB";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private Task Handle(HttpContext context, SsrOptions? options = null)
    {
        var clients = new Mock<IHttpClientFactory>();
        clients.Setup(c => c.CreateClient(SsrPages.ClientName)).Returns(() => new HttpClient(_renderer));
        return SsrPages.Handle(context, _ssr.Object, clients.Object, Options.Create(options ?? new SsrOptions()), NullLogger<SsrServer>.Instance);
    }

    private static string Body(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return new StreamReader(context.Response.Body).ReadToEnd();
    }

    [Test]
    public async Task Handle_RendererRunning_ForwardsThePageRequestAsTheUser()
    {
        var context = Page();

        await Handle(context);

        var forwarded = _renderer.Requests.Should().ContainSingle().Subject;
        forwarded.RequestUri.Should().Be(new Uri(RendererAddress, PagePath + PageQuery));
        forwarded.Headers.GetValues("Cookie").Should().Equal(SessionCookie);
        forwarded.Headers.GetValues("Accept-Language").Should().Equal("en-GB");
        forwarded.Headers.GetValues("X-Forwarded-Proto").Should().Equal("https");
        forwarded.Headers.GetValues("X-Forwarded-Host").Should().Equal("medapp.test");
    }

    [Test]
    public async Task Handle_RendererRunning_SendsBackItsPage()
    {
        var context = Page();

        await Handle(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().StartWith("text/html");
        context.Response.Headers.Vary.ToString().Should().Be("Origin");
        // The connection's own headers are Kestrel's to set.
        context.Response.Headers.ContainsKey("Connection").Should().BeFalse();
        Body(context).Should().Be(RenderedHtml);
    }

    [Test]
    public async Task Handle_RendererRedirects_PassesTheRedirectOn()
    {
        _renderer = new RecordingHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TemporaryRedirect);
            response.Headers.Location = new Uri("/login", UriKind.Relative);
            return response;
        });
        var context = Page();

        await Handle(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status307TemporaryRedirect);
        context.Response.Headers.Location.ToString().Should().Be("/login");
    }

    [Test]
    public async Task Handle_RendererNotRunning_SendsTheClientRenderedApp()
    {
        _ssr.SetupGet(s => s.Address).Returns((Uri?)null);
        var context = Page();

        await Handle(context);

        _renderer.Requests.Should().BeEmpty();
        context.Response.ContentType.Should().Be("text/html; charset=utf-8");
        Body(context).Should().Be(IndexHtml);
    }

    [Test]
    public async Task Handle_RendererFails_SendsTheClientRenderedApp()
    {
        _renderer = new RecordingHandler(_ => throw new HttpRequestException("Connection refused"));
        var context = Page();

        await Handle(context);

        Body(context).Should().Be(IndexHtml);
    }

    [Test]
    public async Task Handle_RendererTooSlow_SendsTheClientRenderedApp()
    {
        _renderer = new RecordingHandler(_ => throw new TaskCanceledException("Timed out"));
        var context = Page();

        await Handle(context, new SsrOptions { RenderTimeout = TimeSpan.FromMilliseconds(1) });

        Body(context).Should().Be(IndexHtml);
    }

    [Test]
    public async Task Handle_NotAPageRead_SendsTheClientRenderedApp()
    {
        var context = Page(HttpMethods.Post);

        await Handle(context);

        _renderer.Requests.Should().BeEmpty();
        Body(context).Should().Be(IndexHtml);
    }

    [Test]
    public async Task Handle_NoAppBuilt_ReturnsNotFound()
    {
        _ssr.SetupGet(s => s.Address).Returns((Uri?)null);
        File.Delete(Path.Combine(_webRoot, "index.html"));
        var context = Page();

        await Handle(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    /// <summary>Answers each request with the response given, recording the requests.</summary>
    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }
}
