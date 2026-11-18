using MedApp.Domain.Contacts;
using MedApp.Domain.Letters;

namespace MedApp.Application.Letters.Common;

/// <summary>A letter's document, filled, with the merge fields that had no value.</summary>
public sealed record FilledLetter(LetterDocument Document, IReadOnlyList<string> Missing);

/// <summary>
/// Turns a letter into the document the renderer makes a PDF of: its text with its merge fields filled from the record
/// (<see cref="LetterRecords.MergeValuesAsync"/>), addressed to its first recipient, the others listed as copies after
/// the closing, and the patient's identifiers on every page (hazard H-50). One PDF for every recipient.
/// </summary>
public static class LetterRendering
{
    /// <summary>The marking on every page of a draft's preview: it isn't the approved letter.</summary>
    public const string DraftMarking = "Draft, not approved";

    public static FilledLetter Fill(Letter letter, IReadOnlyDictionary<string, string?> values, string? marking)
    {
        var letterhead = LetterMergeFields.FillPlain(letter.Letterhead, values);
        var body = LetterMergeFields.FillMarkdown(LetterTemplateRules.Tidy(letter.Body), values);
        var closing = LetterMergeFields.FillPlain(letter.Closing, values);

        var recipients = letter.Recipients.OrderBy(r => r.Position).ToList();
        var closingLines = LetterTemplateRules.Lines(closing.Text).ToList();
        if (recipients.Count > 1)
        {
            closingLines.Add(string.Empty);
            closingLines.Add("Copies to:");
            closingLines.AddRange(recipients.Skip(1).Select(r => r.Name));
        }

        var document = new LetterDocument(
            Title: LetterKinds.Describe(letter.Kind),
            Letterhead: LetterTemplateRules.Lines(letterhead.Text),
            RecipientAddress: recipients.Count == 0 ? [] : AddressBlock(recipients[0]),
            Body: body.Text,
            Closing: closingLines,
            PatientIdentifiers: LetterIdentifiers.Line(
                values.GetValueOrDefault(LetterMergeFields.PatientName) ?? string.Empty,
                values.GetValueOrDefault(LetterMergeFields.PatientDateOfBirth),
                values.GetValueOrDefault(LetterMergeFields.PatientNhsNumber)),
            LargePrint: letter.LargePrint,
            Marking: marking);

        var missing = letterhead.Missing.Concat(body.Missing).Concat(closing.Missing).Distinct(StringComparer.Ordinal).ToList();
        return new FilledLetter(document, missing);
    }

    /// <summary>The recipient's name, address lines and postcode, one per line.</summary>
    public static IReadOnlyList<string> AddressBlock(LetterRecipient recipient) =>
        new[] { recipient.Name }
            .Concat(PostalAddress.Split(recipient.AddressLines))
            .Concat(string.IsNullOrWhiteSpace(recipient.Postcode) ? [] : [recipient.Postcode])
            .ToList();

    /// <summary>Every piece of text that goes on the page, for the font check.</summary>
    public static string AllText(LetterDocument document) =>
        string.Join("\n", document.Letterhead
            .Concat(document.RecipientAddress)
            .Append(document.Body)
            .Concat(document.Closing)
            .Append(document.PatientIdentifiers));

    /// <summary>Why the missing merge fields stop approval, naming each in words.</summary>
    public static string MissingMessage(IReadOnlyList<string> missing)
    {
        var labels = missing
            .Select(name => LetterMergeFields.All.FirstOrDefault(f => f.Name == name)?.Label ?? name)
            .Select(label => label.StartsWith("GP", StringComparison.Ordinal) ? "the " + label : "the " + char.ToLowerInvariant(label[0]) + label[1..]);
        return $"The letter can't be approved: it uses {string.Join(", ", labels)}, which {(missing.Count == 1 ? "has" : "have")} no value for this letter. " +
               "Add it to the record, or take the merge field out of the letter.";
    }

    /// <summary>The PDF's file name: the kind and the date, never the patient.</summary>
    public static string FileName(Letter letter, DateOnly approvedOn) =>
        $"{LetterKinds.Describe(letter.Kind)} {approvedOn:yyyy-MM-dd}.pdf";
}
