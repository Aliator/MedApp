using System.Net;
using System.Text;
using FluentAssertions;
using MedApp.API.ServerRendering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
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
    public async Task Handle_RendererFailsAnyOtherWay_SendsTheClientRenderedApp()
    {
        // E.g. a handler's own exception, such as a circuit breaker's.
        _renderer = new RecordingHandler(_ => throw new InvalidOperationException("Something else went wrong"));
        var context = Page();

        await Handle(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        Body(context).Should().Be(IndexHtml);
    }

    [TestCase(HttpStatusCode.InternalServerError)]
    [TestCase(HttpStatusCode.BadGateway)]
    [TestCase(HttpStatusCode.ServiceUnavailable)]
    public async Task Handle_RendererServerError_SendsTheClientRenderedApp(HttpStatusCode status)
    {
        _renderer = new RecordingHandler(_ =>
        {
            var response = new HttpResponseMessage(status) { Content = new StringContent(RenderedHtml) };
            response.Headers.TryAddWithoutValidation("Vary", "Origin");
            return response;
        });
        var context = Page();

        await Handle(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.Headers.ContainsKey("Vary").Should().BeFalse();
        Body(context).Should().Be(IndexHtml);
    }

    [Test]
    public async Task Handle_RendererNotFound_PassesTheNotFoundPageOn()
    {
        _renderer = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(RenderedHtml, Encoding.UTF8, "text/html"),
        });
        var context = Page();

        await Handle(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        Body(context).Should().Be(RenderedHtml);
    }

    [Test]
    public async Task Handle_RendererFailsBeforeSendingAnything_SendsTheClientRenderedAppWithoutItsHeaders()
    {
        _renderer = new RecordingHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new FailingContent(writeFirst: false) };
            response.Headers.TryAddWithoutValidation("Vary", "Origin");
            return response;
        });
        var context = Page();

        await Handle(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.Headers.ContainsKey("Vary").Should().BeFalse();
        context.Response.Headers.CacheControl.ToString().Should().BeEmpty();
        Body(context).Should().Be(IndexHtml);
    }

    [Test]
    public async Task Handle_RendererFailsPartWay_EndsTheResponseRatherThanThrowing()
    {
        _renderer = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new FailingContent(writeFirst: true) });
        var context = Page();
        var lifetime = new Mock<IHttpRequestLifetimeFeature>();
        lifetime.SetupGet(l => l.RequestAborted).Returns(CancellationToken.None);
        context.Features.Set(lifetime.Object);
        context.Features.Set<IHttpResponseFeature>(new StartedOnceWritten(context.Response.Body));

        await Handle(context);

        lifetime.Verify(l => l.Abort(), Times.Once);
        Body(context).Should().Be(PartialHtml);
    }

    [Test]
    public async Task Handle_BrowserWentAway_DoesNotSendAnything()
    {
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        _renderer = new RecordingHandler(_ => throw new TaskCanceledException("The browser went away"));
        var context = Page();
        context.RequestAborted = aborted.Token;

        var handling = () => Handle(context);

        await handling.Should().ThrowAsync<TaskCanceledException>();
        Body(context).Should().BeEmpty();
    }

    [Test]
    public async Task Handle_PathNamesAnotherHost_StillAsksTheRenderer()
    {
        var context = Page();
        context.Request.Path = OtherHostPath;
        context.Request.QueryString = QueryString.Empty;

        await Handle(context);

        var forwarded = _renderer.Requests.Should().ContainSingle().Subject.RequestUri!;
        forwarded.Scheme.Should().Be(RendererAddress.Scheme);
        forwarded.Host.Should().Be(RendererAddress.Host);
        forwarded.Port.Should().Be(RendererAddress.Port);
        forwarded.AbsolutePath.Should().Be(OtherHostPath);
    }

    [Test]
    public async Task Handle_RendererRunning_TellsBrowsersAndCachesNotToKeepThePage()
    {
        _renderer = new RecordingHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(RenderedHtml) };
            response.Headers.TryAddWithoutValidation("Cache-Control", RendererCacheControl);
            return response;
        });
        var context = Page();

        await Handle(context);

        context.Response.Headers.CacheControl.ToString().Should().Be("no-store");
    }

    [Test]
    public async Task Handle_RendererRedirects_TellsBrowsersAndCachesNotToKeepIt()
    {
        _renderer = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.TemporaryRedirect));
        var context = Page();

        await Handle(context);

        context.Response.Headers.CacheControl.ToString().Should().Be("no-store");
    }

    [Test]
    public async Task Handle_RendererNotRunning_LeavesTheClientRenderedAppsCachingAsItIs()
    {
        _ssr.SetupGet(s => s.Address).Returns((Uri?)null);
        var context = Page();

        await Handle(context);

        context.Response.Headers.CacheControl.ToString().Should().BeEmpty();
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

    /// <summary>A page that fails as it streams: after its first part, or before anything.</summary>
    private sealed class FailingContent(bool writeFirst) : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            if (writeFirst) await stream.WriteAsync(Encoding.UTF8.GetBytes(PartialHtml));
            throw new IOException("The renderer stopped.");
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    /// <summary>A response that has started, as Kestrel's does, once anything has been written to it.</summary>
    private sealed class StartedOnceWritten(Stream body) : HttpResponseFeature
    {
        public override bool HasStarted => body.Length > 0;
    }
}
