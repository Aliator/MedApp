namespace MedApp.Contracts.Letters.Responses;

/// <summary>A letter template.</summary>
public sealed class LetterTemplateResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>What letters from it are for, by name (<c>ClinicLetter</c>, <c>DischargeSummary</c>, ...).</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>The specialty it's for, or null for every specialty.</summary>
    public Guid? SpecialtyId { get; set; }

    /// <summary>That specialty's name, or null for every specialty.</summary>
    public string? SpecialtyName { get; set; }

    public string Letterhead { get; set; } = string.Empty;
    public string BodySkeleton { get; set; } = string.Empty;
    public string Closing { get; set; } = string.Empty;

    /// <summary>The merge fields its text uses, by name, each once, in the order they first appear.</summary>
    public IReadOnlyList<string> MergeFields { get; set; } = [];

    /// <summary>Archived: it can't be chosen for new letters.</summary>
    public bool IsArchived { get; set; }

    /// <summary>No letter has been written from it, so it can be deleted (otherwise only archived).</summary>
    public bool CanBeDeleted { get; set; }

    /// <summary>Pass back as <c>expectedVersion</c> with a change.</summary>
    public int Version { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// A merge field a template's text may hold, written <c>{{name}}</c>: filled from the letter's record when it's
/// rendered. Every one copies identity or administrative data, never a clinical value.
/// </summary>
public sealed class LetterMergeFieldResponse
{
    /// <summary>What goes between the braces: <c>patient.name</c>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>In words: "Patient's name".</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Where its value comes from.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>The made-up value a preview fills it with.</summary>
    public string Example { get; set; } = string.Empty;
}
