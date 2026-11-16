using System.Text;

namespace MedApp.Contracts.Authentication.Responses;

/// <summary>
/// A new authenticator key for the signed-in account, to add to an app: scanned as a QR code or typed in. It's sent
/// with <c>no-store</c>.
/// </summary>
/// <param name="SharedKey">The key, in lower-case groups of four, to type into an app.</param>
/// <param name="AuthenticatorUri">The <c>otpauth://</c> address the QR code holds.</param>
/// <param name="QrCodePath">The QR code as one SVG path, of the dark modules, to draw dark on a light square.</param>
/// <param name="QrCodeSize">The QR code's width and height in the path's units, its quiet zone included.</param>
public sealed record AuthenticatorSetupResponse(
    string SharedKey,
    string AuthenticatorUri,
    string QrCodePath,
    int QrCodeSize)
{
    // Never prints the key, which the address and the QR code also hold.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(SharedKey)} = ***, {nameof(QrCodeSize)} = {QrCodeSize}");
        return true;
    }
}
