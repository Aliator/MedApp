using FluentAssertions;
using MedApp.API.ServerRendering;

namespace MedApp.UnitTests.Api.ServerRendering;

[TestFixture]
public sealed class SsrOptionsTests
{
    [TestCase("http://127.0.0.1:8082", "http://127.0.0.1:8082/")]
    [TestCase("http://127.0.0.1:8082/", "http://127.0.0.1:8082/")]
    [TestCase("http://localhost:8082", "http://localhost:8082/")]
    [TestCase("https://pages.medapp.test", "https://pages.medapp.test/")]
    [TestCase("http://[::1]:8082", "http://[::1]:8082/")]
    public void ExternalServer_ServerAddress_ReturnsItEndingInASlash(string url, string expected)
    {
        var options = new SsrOptions { ServerUrl = url };

        options.ExternalServer().Should().Be(new Uri(expected));
        options.HasValidServerUrl().Should().BeTrue();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void ExternalServer_NotSet_ReturnsNullAndIsValid(string? url)
    {
        var options = new SsrOptions { ServerUrl = url };

        options.ExternalServer().Should().BeNull();
        options.HasValidServerUrl().Should().BeTrue();
    }

    [TestCase("127.0.0.1:8082")]
    [TestCase("not an address")]
    [TestCase("/pages")]
    [TestCase("ftp://127.0.0.1:8082")]
    [TestCase("file:///app/ssr/server.mjs")]
    [TestCase("http://127.0.0.1:8082/pages")]
    [TestCase("http://127.0.0.1:8082/?x=1")]
    [TestCase("http://127.0.0.1:8082/#top")]
    [TestCase("http://someone:secret@127.0.0.1:8082")]
    public void ExternalServer_NotAServersAddress_ReturnsNullAndIsInvalid(string url)
    {
        var options = new SsrOptions { ServerUrl = url };

        options.ExternalServer().Should().BeNull();
        options.HasValidServerUrl().Should().BeFalse();
    }

    [Test]
    public void Defaults_StartNodeFromOutsideTheWebRoot()
    {
        var options = new SsrOptions();

        options.Enabled.Should().BeTrue();
        options.ServerUrl.Should().BeNull();
        options.ScriptPath.Should().Be("ssr/server.mjs");
        options.RenderTimeout.Should().Be(TimeSpan.FromSeconds(15));
    }
}
