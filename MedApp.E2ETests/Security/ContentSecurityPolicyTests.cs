using System.Text.RegularExpressions;
using MedApp.E2ETests.Common;

namespace MedApp.E2ETests.Security;

/// <summary>
/// The page Content Security Policy's nonce, end to end: each main page is rendered on the server with the nonce of
/// its own response, which the API passes to the page server (whether that runs inside the API's container or in a
/// container of its own beside it). <see cref="E2ETest"/> fails the test if anything on the page then breaks the
/// policy. Against the development server, which sends no policy, it's skipped.
/// </summary>
[TestFixture]
public sealed class ContentSecurityPolicyTests : E2ETest
{
    [TestCase("/")]
    [TestCase("/patients")]
    [TestCase("/procedures")]
    [TestCase("/tasks")]
    [TestCase("/account")]
    [TestCase("/admin/users")]
    [TestCase("/admin/insights")]
    public async Task Page_RenderedOnTheServer_UsesItsResponsesNonce(string path)
    {
        var response = await Page.GotoAsync(path);

        Assert.That(response, Is.Not.Null);
        var policy = await response!.HeaderValueAsync("content-security-policy");
        if (policy is null)
            Assert.Ignore("The app sends no Content Security Policy here (the development server).");
        var nonce = Regex.Match(policy, "'nonce-([^']+)'").Groups[1].Value;
        Assert.That(nonce, Is.Not.Empty, "The page's policy has no nonce.");
        // Rendered on the server (the client-rendered fallback has no nonce in it), with this response's nonce.
        Assert.That(await response.TextAsync(), Does.Contain($"nonce=\"{nonce}\""));

        // Hydrated with the page's scripts running under the policy (a violation fails the test in E2ETest).
        await WaitUntilInteractiveAsync();
        Assert.That(Page.Url, Does.Not.Contain("/login"), "The page sent the E2E account to sign in.");
    }
}
