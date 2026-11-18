namespace MedApp.Contracts.Letters.Requests;

/// <summary>A letter template to add.</summary>
public sealed class CreateLetterTemplateRequest
{
    /// <summary>Required, at most 200 characters; unique within its team (or among those for every team).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// What letters from it are for, by name: <c>ClinicLetter</c>, <c>DischargeSummary</c>, <c>ReferralResponse</c>,
    /// <c>InformationRequest</c>, <c>AppointmentLetter</c> or <c>Other</c>.
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>The team it's for, or null for every team. An archived one can't be chosen.</summary>
    public Guid? TeamId { get; set; }

    /// <summary>Plain lines at the top of the first page (at most 1,000 characters), with merge fields if wanted.</summary>
    public string Letterhead { get; set; } = string.Empty;

    /// <summary>The body letters start from, in Markdown (at most 20,000 characters), with merge fields if wanted.</summary>
    public string BodySkeleton { get; set; } = string.Empty;

    /// <summary>Plain lines after the body (at most 1,000 characters), with merge fields if wanted.</summary>
    public string Closing { get; set; } = string.Empty;
}

/// <summary>A letter template's new details, from the version the caller saw.</summary>
public sealed class UpdateLetterTemplateRequest
{
    /// <summary>Required, at most 200 characters; unique within its team (or among those for every team).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>What letters from it are for, by name (as <see cref="CreateLetterTemplateRequest.Kind"/>).</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>The team it's for, or null for every team. An archived one can only stay.</summary>
    public Guid? TeamId { get; set; }

    public string Letterhead { get; set; } = string.Empty;
    public string BodySkeleton { get; set; } = string.Empty;
    public string Closing { get; set; } = string.Empty;

    /// <summary>The <c>version</c> the caller saw: if anyone has changed the template since, it's a 409.</summary>
    public int ExpectedVersion { get; set; }
}

/// <summary>Archiving or restoring a letter template, from the version the caller saw.</summary>
public sealed class LetterTemplateVersionRequest
{
    /// <summary>The <c>version</c> the caller saw: if anyone has changed the template since, it's a 409.</summary>
    public int ExpectedVersion { get; set; }
}

/// <summary>
/// A template's text to preview as a PDF, saved or not, filled with made-up details: every page is marked as a
/// preview.
/// </summary>
public sealed class PreviewLetterTemplateRequest
{
    /// <summary>The template's name, used only for the PDF's title (at most 200 characters; optional).</summary>
    public string? Name { get; set; }

    public string Letterhead { get; set; } = string.Empty;
    public string BodySkeleton { get; set; } = string.Empty;
    public string Closing { get; set; } = string.Empty;

    /// <summary>In large print (16 point), as a letter can be for the Accessible Information Standard.</summary>
    public bool LargePrint { get; set; }
}
