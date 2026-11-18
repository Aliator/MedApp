namespace MedApp.Application.Letters.Common;

/// <summary>
/// A letter ready to render, its merge fields already filled (<see cref="LetterMergeFields"/>).
/// </summary>
/// <param name="Title">The PDF's title (its document properties): the kind of letter, never the patient.</param>
/// <param name="Letterhead">Plain lines at the top of the first page.</param>
/// <param name="RecipientAddress">The recipient's name and address lines, under the letterhead (none for a preview).</param>
/// <param name="Body">The body, in Markdown: headings, paragraphs, emphasis, lists. HTML in it is shown as text.</param>
/// <param name="Closing">Plain lines after the body.</param>
/// <param name="PatientIdentifiers">
/// The patient's name, date of birth and NHS number, on every page beside "Page n of m" (hazard H-50).
/// </param>
/// <param name="LargePrint">16 point instead of 11, for the Accessible Information Standard.</param>
/// <param name="Marking">
/// Words shown prominently on every page when it isn't the approved letter ("Draft, not approved"), or null.
/// </param>
public sealed record LetterDocument(
    string Title,
    IReadOnlyList<string> Letterhead,
    IReadOnlyList<string> RecipientAddress,
    string Body,
    IReadOnlyList<string> Closing,
    string PatientIdentifiers,
    bool LargePrint,
    string? Marking);

/// <summary>
/// Makes a letter's PDF (decision 98: PDFsharp and MigraDoc, in a bundled font, on the server). Implemented in
/// Infrastructure (<c>PdfSharpLetterRenderer</c>).
/// </summary>
public interface ILetterRenderer
{
    /// <summary>
    /// The letter as a PDF file's bytes. A few run at a time in each copy of the API; the rest wait. A character the
    /// font can't show is drawn as "?": check text first with <see cref="FindUnsupportedCharacters"/>.
    /// </summary>
    Task<byte[]> RenderAsync(LetterDocument letter, CancellationToken ct);

    /// <summary>
    /// The characters in a text the letter font can't show (each once, in order; spaces and line breaks are fine), so
    /// they can be refused before a letter is made rather than printed as "?".
    /// </summary>
    IReadOnlyList<string> FindUnsupportedCharacters(string? text);
}
