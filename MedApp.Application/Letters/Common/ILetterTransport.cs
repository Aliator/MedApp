namespace MedApp.Application.Letters.Common;

/// <summary>
/// A letter by secure email: to one address in an allowed domain, with the approved PDF attached. The subject and body
/// hold no patient or clinical text (<see cref="SecureLetterEmails"/>); the PDF is the letter.
/// </summary>
public sealed record SecureLetterEmail(string To, string Subject, string TextBody, string FileName, byte[] Pdf);

/// <summary>
/// Sends letters by secure email (decision 103): <c>SecureEmailLetterTransport</c>, MailKit straight to the relay in
/// <c>Letters:SecureEmail</c>, <b>synchronously</b>, unlike the best-effort email queue: a recipient is marked sent only
/// when the mail server accepts the message, and a failure is shown at once.
/// </summary>
public interface ILetterTransport
{
    /// <summary>Whether letters can be sent this way here (configured, and the relay declared to meet DCB1596).</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Sends it, returning the mail server's reference for it (its message ID). Throws
    /// <see cref="LetterTransportException"/> when the server can't be reached or doesn't accept it.
    /// </summary>
    Task<string> SendAsync(SecureLetterEmail email, CancellationToken ct);
}

/// <summary>The secure email relay couldn't be reached, or didn't accept a letter. Its message holds no address or text.</summary>
public sealed class LetterTransportException(string category, Exception? inner = null)
    : Exception($"The letter wasn't accepted by the secure email relay ({category}).", inner)
{
    /// <summary>What went wrong, without the address or the server's words (they can quote it): e.g. "connection", "refused".</summary>
    public string Category { get; } = category;
}

/// <summary>The words of a letter's secure email: no patient, no clinical text, no member of staff.</summary>
public static class SecureLetterEmails
{
    /// <summary>The PDF's name in the email: never the patient's name.</summary>
    public const string FileName = "letter.pdf";

    public static string Subject(string centre) => $"A letter from {centre}";

    public static string Body(string centre) =>
        $"A letter from {centre}. It's attached.\n\nIf it has reached you by mistake, tell {centre} and delete it.";

    public static SecureLetterEmail For(string to, string centre, byte[] pdf) =>
        new(to, Subject(centre), Body(centre), FileName, pdf);
}
