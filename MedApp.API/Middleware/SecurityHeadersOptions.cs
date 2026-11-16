namespace MedApp.API.Middleware;

/// <summary>The "SecurityHeaders" configuration section (<see cref="SecurityHeaders"/>).</summary>
public sealed class SecurityHeadersOptions
{
    public const string Section = "SecurityHeaders";

    /// <summary>
    /// Sends the pages' Content Security Policy as <c>Content-Security-Policy-Report-Only</c>: browsers report what
    /// it would block (in their console) but block nothing. Off by default. A way out, without a new build, if the
    /// policy ever blocks something the app needs; it isn't meant to stay on.
    /// </summary>
    public bool PageCspReportOnly { get; set; }
}
