using System.Text;
using MedApp.Application.Attachments.Common;
using Microsoft.Net.Http.Headers;

namespace MedApp.API.Attachments;

/// <summary>
/// The headers every attached file (and thumbnail) is sent with, so a browser only ever saves it and never shows or
/// runs it on the app's origin:
/// <list type="bullet">
/// <item><c>Content-Disposition: attachment</c>, with the name as plain ASCII (<c>filename</c>) and exactly
/// (<c>filename*</c>, RFC 5987: UTF-8, percent-encoded), so nothing in it can break out of the header.</item>
/// <item>The type found from the file's contents, never what the uploader's browser said, with <c>nosniff</c> so the
/// browser doesn't guess another.</item>
/// <item><c>Content-Security-Policy: sandbox; default-src 'none'</c>: opened directly, it's a page with no origin that
/// may load and run nothing.</item>
/// <item><c>Cache-Control: no-store</c> (it's patient data) and <c>Cross-Origin-Resource-Policy: same-origin</c> (no
/// other site may embed it).</item>
/// </list>
/// Thumbnails are shown in the app's own <c>&lt;img&gt;</c> tags, which ignore <c>Content-Disposition</c>; nothing
/// else of an upload is ever shown inline.
/// </summary>
public static class AttachmentResponseHeaders
{
    public const string ContentSecurityPolicy = "sandbox; default-src 'none'";

    /// <summary>The request header an upload's file name comes in, percent-encoded UTF-8 (JavaScript's <c>encodeURIComponent</c>).</summary>
    public const string FileNameHeader = "X-File-Name";

    public static void Apply(HttpResponse response, AttachmentDownload download)
    {
        var headers = response.Headers;
        headers.ContentType = download.ContentType;
        headers.ContentDisposition = ContentDisposition(download.FileName);
        headers.XContentTypeOptions = "nosniff";
        headers.CacheControl = "no-store";
        headers.ContentSecurityPolicy = ContentSecurityPolicy;
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
        headers.XFrameOptions = "DENY";
        if (download.Length is { } length)
            response.ContentLength = length;
    }

    /// <summary><c>attachment; filename="ascii"; filename*=UTF-8''percent-encoded</c>.</summary>
    public static string ContentDisposition(string fileName) =>
        $"attachment; filename=\"{AttachmentFileName.AsciiFallback(fileName)}\"; filename*=UTF-8''{Rfc5987(fileName)}";

    /// <summary>
    /// An upload's name from its header: percent-decoded, or as it came if it isn't valid percent-encoding. Kestrel
    /// refuses headers that aren't ASCII, so a name outside ASCII has to be encoded.
    /// </summary>
    public static string? DecodeFileName(string? header)
    {
        if (header is null)
            return null;

        try
        {
            return Uri.UnescapeDataString(header);
        }
        catch (UriFormatException)
        {
            return header;
        }
    }

    /// <summary>RFC 5987's <c>value-chars</c>: UTF-8, with everything but its <c>attr-char</c>s percent-encoded.</summary>
    private static string Rfc5987(string value)
    {
        var encoded = new StringBuilder(value.Length * 3);
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            var c = (char)b;
            if (char.IsAsciiLetterOrDigit(c) || c is '!' or '#' or '$' or '&' or '+' or '-' or '.' or '^' or '_' or '`' or '|' or '~')
                encoded.Append(c);
            else
                encoded.Append('%').Append(b.ToString("X2"));
        }

        return encoded.ToString();
    }
}
