using FluentAssertions;
using MedApp.API.ServerRendering;
using Microsoft.AspNetCore.Http;

namespace MedApp.UnitTests.Api.ServerRendering;

[TestFixture]
public sealed class PageServerFilesTests
{
    [TestCase("/ssr")]
    [TestCase("/ssr/server.mjs")]
    [TestCase("/SSR/server.mjs")]
    [TestCase("/.vite/manifest.json")]
    [TestCase("//ssr/server.mjs")]
    [TestCase("//evil.example/x")]
    [TestCase("/a//b")]
    public void IsBlocked_ServerFilesOrAnEmptySegment_ReturnsTrue(string path)
    {
        PageServerFiles.IsBlocked(new PathString(path)).Should().BeTrue();
    }

    [TestCase("")]
    [TestCase("/")]
    [TestCase("/patients")]
    [TestCase("/patients/")]
    [TestCase("/api/x")]
    [TestCase("/assets/a.js")]
    [TestCase("/ssrish")]
    public void IsBlocked_PagesFilesAndApi_ReturnsFalse(string path)
    {
        PageServerFiles.IsBlocked(new PathString(path)).Should().BeFalse();
    }
}
