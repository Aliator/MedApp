using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MedApp.Application.Letters.Common;

/// <summary>
/// A merge field: a name a template's text holds between double braces (<c>{{patient.name}}</c>), filled from the
/// letter's record when it's rendered.
/// </summary>
/// <param name="Name">What goes between the braces.</param>
/// <param name="Label">In words.</param>
/// <param name="Description">Where its value comes from.</param>
/// <param name="Example">The made-up value a preview fills it with: synthetic, never a real person's.</param>
public sealed record LetterMergeField(string Name, string Label, string Description, string Example);

/// <summary>A text with its merge fields filled, and the fields that had no value (left empty in the text).</summary>
public sealed record LetterMergeResult(string Text, IReadOnlyList<string> Missing);

/// <summary>
/// The fixed list of merge fields (decision 99 of docs/centre-phases-3-5-plan.md), and how they're found and filled.
/// <para>
/// <b>They copy identity and administrative data only:</b> the patient's name, date of birth, NHS number and address,
/// their practice's name and address, dates, staff names and job titles, and the site. No field reads a clinical value,
/// and none is ever added that does: the clinical content of a letter is what its author writes (the device position,
/// docs/clinical-safety/intended-purpose-and-device-decision.md).
/// </para>
/// <para>
/// A name is letters, digits and dots, case-sensitive, with optional spaces inside the braces (<c>{{ today }}</c>). Text
/// in double braces that isn't a known name is refused when a template is saved (400), so a typo can't reach a letter.
/// </para>
/// </summary>
public static class LetterMergeFields
{
    public const string PatientName = "patient.name";
    public const string PatientDateOfBirth = "patient.dateOfBirth";
    public const string PatientNhsNumber = "patient.nhsNumber";
    public const string PatientAddress = "patient.address";
    public const string PracticeName = "practice.name";
    public const string PracticeAddress = "practice.address";
    public const string ReferralReceivedDate = "referral.receivedDate";
    public const string LastAttendedAppointmentDate = "appointment.lastAttendedDate";
    public const string AuthorName = "author.name";
    public const string AuthorJobTitle = "author.jobTitle";
    public const string ApproverName = "approver.name";
    public const string ApproverJobTitle = "approver.jobTitle";
    public const string Today = "today";
    public const string SiteName = "site.name";

    /// <summary>Every field, in the order the template page lists them.</summary>
    public static readonly IReadOnlyList<LetterMergeField> All =
    [
        new(PatientName, "Patient's name", "The patient's full name, as on their record.", "Sam Example"),
        new(PatientDateOfBirth, "Patient's date of birth", "The patient's date of birth.", "1 January 1970"),
        new(PatientNhsNumber, "Patient's NHS number", "The patient's NHS number, in groups of three, three and four.", "999 000 0018"),
        new(PatientAddress, "Patient's address", "The patient's address and postcode, on separate lines.", "1 Example Street\nExampletown\nZZ1 1ZZ"),
        new(PracticeName, "GP practice's name", "The name of the GP practice the patient is registered with.", "Example Medical Practice"),
        new(PracticeAddress, "GP practice's address", "That practice's address and postcode, on separate lines.", "2 Sample Road\nExampletown\nZZ1 2ZZ"),
        new(ReferralReceivedDate, "Referral received", "The date the referral the letter is about was received.", "3 March 2026"),
        new(LastAttendedAppointmentDate, "Last appointment attended", "The date of the patient's last attended appointment on the pathway.", "10 March 2026"),
        new(AuthorName, "Author's name", "The full name of the member of staff who wrote the letter.", "Alex Author"),
        new(AuthorJobTitle, "Author's job title", "Their job title.", "Consultant Dermatologist"),
        new(ApproverName, "Approver's name", "The full name of the member of staff who approved the letter.", "Jo Approver"),
        new(ApproverJobTitle, "Approver's job title", "Their job title.", "Consultant Dermatologist"),
        new(Today, "Today's date", "The date the letter is rendered.", "18 November 2026"),
        new(SiteName, "Site's name", "The name of the site the letter is sent from.", "Main site"),
    ];

    private static readonly IReadOnlyDictionary<string, LetterMergeField> ByName =
        All.ToDictionary(f => f.Name, StringComparer.Ordinal);

    /// <summary>Double braces round a name, with optional spaces: <c>{{patient.name}}</c> or <c>{{ today }}</c>.</summary>
    private static readonly Regex Field = new(
        @"\{\{[ \t]*([A-Za-z0-9.]+)[ \t]*\}\}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly CultureInfo British = CultureInfo.GetCultureInfo("en-GB");

    /// <summary>The UK long date format letters use: "1 January 1970".</summary>
    public static string FormatDate(DateOnly date) => date.ToString("d MMMM yyyy", British);

    public static bool IsKnown(string name) => ByName.ContainsKey(name);

    /// <summary>The names in a text, known or not, each once, in the order they first appear.</summary>
    public static IReadOnlyList<string> Find(string? text) =>
        string.IsNullOrEmpty(text)
            ? []
            : Field.Matches(text).Select(m => m.Groups[1].Value).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>The known names several texts use, each once, in the order they first appear.</summary>
    public static IReadOnlyList<string> Used(params string?[] texts) =>
        texts.SelectMany(Find).Where(IsKnown).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>The names in a text that aren't merge fields, each once.</summary>
    public static IReadOnlyList<string> Unknown(string? text) => Find(text).Where(n => !IsKnown(n)).ToList();

    /// <summary>Why a text's merge fields can't be saved, or null: "{{patient.nme}} isn't a merge field. ..."</summary>
    public static string? UnknownMessage(string? text)
    {
        var unknown = Unknown(text).Select(n => "{{" + n + "}}").ToList();
        return unknown.Count switch
        {
            0 => null,
            1 => $"{unknown[0]} isn't a merge field. Choose one from the list, or remove the double braces.",
            _ => $"{string.Join(", ", unknown)} aren't merge fields. Choose them from the list, or remove the double braces.",
        };
    }

    /// <summary>The made-up values a preview fills every field with (today's date is the real one).</summary>
    public static IReadOnlyDictionary<string, string?> SampleValues(DateOnly today) =>
        All.ToDictionary(
            f => f.Name,
            f => f.Name == Today ? FormatDate(today) : (string?)f.Example,
            StringComparer.Ordinal);

    /// <summary>
    /// Fills a plain text's merge fields (the letterhead or closing). A field with no value is left empty, and listed in
    /// <see cref="LetterMergeResult.Missing"/>: a letter with one can't be approved (PR 16).
    /// </summary>
    public static LetterMergeResult FillPlain(string? text, IReadOnlyDictionary<string, string?> values) =>
        Fill(text, values, value => string.Join("\n", SplitLines(value)));

    /// <summary>
    /// Fills a Markdown text's merge fields (the body). Each value is escaped, so a name with an asterisk or a bracket
    /// is shown as it is and can't change the letter's formatting; a value's lines are kept as lines (hard breaks).
    /// </summary>
    public static LetterMergeResult FillMarkdown(string? text, IReadOnlyDictionary<string, string?> values) =>
        Fill(text, values, value => string.Join("\\\n", SplitLines(value).Select(EscapeMarkdown)));

    /// <summary>A value's lines, trimmed, without empty ones.</summary>
    public static IReadOnlyList<string> SplitLines(string? value) =>
        string.IsNullOrEmpty(value)
            ? []
            : value.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

    /// <summary>A plain value as Markdown that shows exactly it: every ASCII punctuation or symbol character escaped.</summary>
    public static string EscapeMarkdown(string value)
    {
        var escaped = new StringBuilder(value.Length * 2);
        foreach (var c in value)
        {
            if (char.IsAscii(c) && (char.IsPunctuation(c) || char.IsSymbol(c)))
                escaped.Append('\\');
            escaped.Append(c);
        }

        return escaped.ToString();
    }

    private static LetterMergeResult Fill(
        string? text, IReadOnlyDictionary<string, string?> values, Func<string, string> format)
    {
        if (string.IsNullOrEmpty(text))
            return new LetterMergeResult(string.Empty, []);

        var missing = new List<string>();
        var filled = Field.Replace(text, match =>
        {
            var name = match.Groups[1].Value;
            // Unknown names can't be saved in a template; any in a letter's own text stay as they are.
            if (!IsKnown(name))
                return match.Value;
            if (values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
                return format(value.Trim());
            if (!missing.Contains(name))
                missing.Add(name);
            return string.Empty;
        });

        return new LetterMergeResult(filled, missing);
    }
}
