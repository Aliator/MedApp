using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using MedApp.E2ETests.Common;

namespace MedApp.E2ETests.Security;

/// <summary>
/// The page Content Security Policy against the real browser: each page is rendered on the server with the nonce of
/// its own response (passed by the API to the page server, whether that runs inside the API's container or beside
/// it), and nothing on it breaks the policy. Against the development server, which sends no policy, it's skipped.
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
    public async Task Page_RenderedOnTheServer_UsesItsResponsesNonceAndBreaksNothing(string path)
    {
        var violations = new ConcurrentQueue<string>();
        await Page.ExposeFunctionAsync("medappCspViolation", (string violation) => violations.Enqueue(violation));
        await Page.AddInitScriptAsync("""
            document.addEventListener('securitypolicyviolation', (e) =>
              window.medappCspViolation(`${e.violatedDirective} ${e.blockedURI} ${e.sourceFile}:${e.lineNumber}`));
            """);
        Page.Console += (_, message) =>
        {
            if (message.Type == "error" && message.Text.Contains("Content Security Policy", StringComparison.Ordinal))
                violations.Enqueue(message.Text);
        };

        var response = await Page.GotoAsync(path);

        Assert.That(response, Is.Not.Null);
        var policy = await response!.HeaderValueAsync("content-security-policy");
        if (policy is null)
            Assert.Ignore("The app sends no Content Security Policy here (the development server).");
        var nonce = Regex.Match(policy, "'nonce-([^']+)'").Groups[1].Value;
        Assert.That(nonce, Is.Not.Empty, "The page's policy has no nonce.");
        // Rendered on the server (the client-rendered fallback has no nonce in it), with this response's nonce.
        Assert.That(await response.TextAsync(), Does.Contain($"nonce=\"{nonce}\""));

        await WaitUntilInteractiveAsync();

        Assert.That(violations, Is.Empty);
        Assert.That(Page.Url, Does.Not.Contain("/login"), "The page sent the E2E account to sign in.");
    }
}
