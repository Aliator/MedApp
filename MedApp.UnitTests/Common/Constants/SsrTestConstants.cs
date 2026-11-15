namespace MedApp.UnitTests.Common.Constants;

public static class SsrTestConstants
{
    public static readonly Uri RendererAddress = new("http://127.0.0.1:61234/");
    public const string PagePath = "/patients";
    public const string PageQuery = "?last=bennett";
    public const string SessionCookie = "medapp_session=synthetic";
    public const string RenderedHtml = "<!DOCTYPE html><html><body>Rendered on the server</body></html>";
    public const string IndexHtml = "<!doctype html><html><body><div id=\"root\"></div></body></html>";
}
