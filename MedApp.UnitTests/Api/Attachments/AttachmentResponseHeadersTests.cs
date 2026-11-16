using FluentAssertions;
using MedApp.API.Attachments;
using MedApp.Application.Attachments.Common;
using Microsoft.AspNetCore.Http;

namespace MedApp.UnitTests.Api.Attachments;

[TestFixture]
public sealed class AttachmentResponseHeadersTests
{
    [Test]
    public void Apply_Download_SetsEveryProtectiveHeader()
    {
        var context = new DefaultHttpContext();

        AttachmentResponseHeaders.Apply(context.Response, new AttachmentDownload("scan.pdf", "application/pdf", 1234, Stream.Null));

        var headers = context.Response.Headers;
        headers.ContentType.ToString().Should().Be("application/pdf");
        headers.ContentDisposition.ToString().Should().Be("attachment; filename=\"scan.pdf\"; filename*=UTF-8''scan.pdf");
        headers.XContentTypeOptions.ToString().Should().Be("nosniff");
        headers.CacheControl.ToString().Should().Be("no-store");
        headers.ContentSecurityPolicy.ToString().Should().Be("sandbox; default-src 'none'");
        headers["Cross-Origin-Resource-Policy"].ToString().Should().Be("same-origin");
        headers.XFrameOptions.ToString().Should().Be("DENY");
        context.Response.ContentLength.Should().Be(1234);
    }

    [Test]
    public void Apply_UnknownLength_SetsNoLength()
    {
        var context = new DefaultHttpContext();

        AttachmentResponseHeaders.Apply(context.Response, new AttachmentDownload("thumbnail.png", "image/png", null, Stream.Null));

        context.Response.ContentLength.Should().BeNull();
    }

    [Test]
    public void Apply_OverridesAStricterOrLaxerPolicySetBefore()
    {
        var context = new DefaultHttpContext();
        context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

        AttachmentResponseHeaders.Apply(context.Response, new AttachmentDownload("a.txt", "text/plain; charset=utf-8", 1, Stream.Null));

        context.Response.Headers.ContentSecurityPolicy.ToString().Should().Be(AttachmentResponseHeaders.ContentSecurityPolicy);
    }

    [TestCase("résumé.pdf", "attachment; filename=\"r_sum_.pdf\"; filename*=UTF-8''r%C3%A9sum%C3%A9.pdf")]
    [TestCase("a \"quoted\"; name=x.pdf", "attachment; filename=\"a _quoted_; name=x.pdf\"; filename*=UTF-8''a%20%22quoted%22%3B%20name%3Dx.pdf")]
    [TestCase("back\\slash.pdf", "attachment; filename=\"back_slash.pdf\"; filename*=UTF-8''back%5Cslash.pdf")]
    [TestCase("報告.pdf", "attachment; filename=\"__.pdf\"; filename*=UTF-8''%E5%A0%B1%E5%91%8A.pdf")]
    [TestCase("a!#$&+-.^_`|~b.txt", "attachment; filename=\"a!#$&+-.^_`|~b.txt\"; filename*=UTF-8''a!#$&+-.^_`|~b.txt")]
    public void ContentDisposition_Name_IsSafeAsciiAndExactUtf8(string name, string expected)
    {
        AttachmentResponseHeaders.ContentDisposition(name).Should().Be(expected);
    }

    [Test]
    public void ContentDisposition_HeaderInjection_StaysInsideTheValue()
    {
        var value = AttachmentResponseHeaders.ContentDisposition("evil\r\nSet-Cookie: x=y.pdf");

        value.Should().NotContain("\r").And.NotContain("\n");
    }

    [TestCase(null, null)]
    [TestCase("scan.pdf", "scan.pdf")]
    [TestCase("r%C3%A9sum%C3%A9.pdf", "résumé.pdf")]
    [TestCase("100%25.txt", "100%.txt")]
    [TestCase("bad%ZZ.txt", "bad%ZZ.txt")]
    public void DecodeFileName_PercentEncoded_IsDecoded(string? header, string? expected)
    {
        AttachmentResponseHeaders.DecodeFileName(header).Should().Be(expected);
    }
}
