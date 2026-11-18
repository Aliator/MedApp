namespace MedApp.Domain.Letters;

/// <summary>
/// A letter template (decision 99 of docs/centre-phases-3-5-plan.md): what a letter of one kind starts from. Holders of
/// ManageSetup keep them. A letter will copy its template's text when it's started (PR 16), so editing a template
/// changes only letters started afterwards. Its text may hold merge fields (<c>{{patient.name}}</c>), filled from the
/// record when a letter is rendered; they copy identity and administrative data only, never a clinical value. A
/// template holds no patient data: it's an organisation's wording.
/// </summary>
public sealed class LetterTemplate
{
    public const int MaxNameLength = 200;

    /// <summary>The letterhead's and closing's longest: a few plain lines.</summary>
    public const int MaxLetterheadLength = 1000;

    public const int MaxClosingLength = 1000;

    /// <summary>A body skeleton's longest, the same as a letter's body (decision 100).</summary>
    public const int MaxBodyLength = 20000;

    public Guid Id { get; init; }

    /// <summary>Unique within its specialty (or among the templates for every specialty), ignoring case.</summary>
    public string Name { get; set; } = string.Empty;

    public LetterKind Kind { get; set; }

    /// <summary>The specialty whose letters it's for, or null for every specialty.</summary>
    public Guid? SpecialtyId { get; set; }

    /// <summary>
    /// Plain lines at the top of the first page: the centre's name, address and phone. Merge fields are filled; no
    /// Markdown.
    /// </summary>
    public string Letterhead { get; set; } = string.Empty;

    /// <summary>The body the author starts from, in Markdown: the headings they fill in.</summary>
    public string BodySkeleton { get; set; } = string.Empty;

    /// <summary>Plain lines after the body: the sign-off, with the author's name and job title as merge fields.</summary>
    public string Closing { get; set; } = string.Empty;

    /// <summary>Archived templates can't be chosen for new letters; letters written from one keep their copy.</summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// One more for each change. A change names the version it started from, so a page loaded before someone else's
    /// change is refused (409); it's also a concurrency token, so two saves at once can't both win.
    /// </summary>
    public int Version { get; set; }

    public DateTime CreatedAt { get; init; }
    public DateTime LastUpdated { get; set; }
}
