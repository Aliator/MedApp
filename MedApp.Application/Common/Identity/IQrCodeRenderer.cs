namespace MedApp.Application.Common.Identity;

/// <summary>Draws text as a QR code (for setting up an authenticator app).</summary>
public interface IQrCodeRenderer
{
    QrCodeImage Render(string text);
}

/// <summary>
/// A QR code as one SVG path: <paramref name="Path"/> draws the dark modules in a square of <paramref name="Size"/> units,
/// a quiet zone included, for an <c>&lt;svg viewBox="0 0 Size Size"&gt;</c> on a light background.
/// </summary>
public sealed record QrCodeImage(string Path, int Size);
