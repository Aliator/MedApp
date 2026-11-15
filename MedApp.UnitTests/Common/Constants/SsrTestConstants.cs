namespace MedApp.UnitTests.Common.Constants;

public static class SsrTestConstants
{
    public static readonly Uri RendererAddress = new("http://127.0.0.1:61234/");
    public const string PagePath = "/patients";
    public const string PageQuery = "?last=bennett";
    public const string SessionCookie = "medapp_session=synthetic";
    public const string RenderedHtml = "<!DOCTYPE html><html><body>Rendered on the server</body></html>";
    public const string IndexHtml = "<!doctype html><html><body><div id=\"root\"></div></body></html>";

    /// <summary>A path that, resolved against the renderer's address as a URL, names another host.</summary>
    public const string OtherHostPath = "//evil.example/x";
    public const string RendererCacheControl = "public, max-age=600";
    public const string PartialHtml = "<!DOCTYPE html><html><body>";

    // Starting Node: what it's given, and the API's own settings it mustn't be.
    public const string NodePath = "node";
    public const string NodeScript = "/srv/medapp/wwwroot/ssr/server.mjs";
    public const string NodeScriptDirectory = "/srv/medapp/wwwroot/ssr";
    public const int NodePort = 61234;
    public const string ApiUrl = "http://127.0.0.1:5090";
    public const string RendererKey = "synthetic-renderer-key";
    public const string SearchPath = "/usr/local/bin:/usr/bin";
    public const string NodeTimeZone = "Europe/London";
    public const string WindowsRoot = @"C:\Windows";
    public const string ConnectionStringVariable = "ConnectionStrings__DefaultConnection";
    public const string ConnectionString = "Server=sql.test;Database=MedApp;User ID=sa;Password=synthetic-secret";
    public const string BackplaneVariable = "ConnectionStrings__SignalRBackplane";
    public const string BackplaneConnectionString = "redis.test:6379,password=synthetic-secret";
    public const string FirstAdminPasswordVariable = "FirstAdmin__Password";
}
