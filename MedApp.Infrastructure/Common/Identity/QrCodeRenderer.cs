using MedApp.Application.Common.Identity;
using Net.Codecrete.QrCodeGenerator;

namespace MedApp.Infrastructure.Common.Identity;

/// <summary>
/// Draws QR codes with Net.Codecrete.QrCodeGenerator (MIT; a port of Project Nayuki's reference generator, with no
/// dependencies), on the server: the page gets one SVG path and draws it, so the web app needs no QR library, and the
/// image is plain markup the Content Security Policy allows (no script, no data: URL).
/// </summary>
public sealed class QrCodeRenderer : IQrCodeRenderer
{
    /// <summary>The quiet zone around the code, in modules, as the QR standard asks.</summary>
    public const int Border = 4;

    public QrCodeImage Render(string text)
    {
        var qr = QrCode.EncodeText(text, QrCode.Ecc.Medium);
        return new QrCodeImage(qr.ToGraphicsPath(Border), qr.Size + Border * 2);
    }
}
