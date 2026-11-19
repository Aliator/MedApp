namespace MedApp.Domain.Specimens;

/// <summary>
/// A kind of specimen (Skin biopsy (punch), Urine), with how many days the lab is expected to take to report one.
/// Organisation-wide setup, kept by holders of ManageSetup; one in use is archived, not deleted. Its name is
/// admin-written, so notifications may quote it.
/// </summary>
public sealed class SpecimenType
{
    public const int MaxNameLength = 100;
    public const int MinTurnaroundDays = 1;
    public const int MaxTurnaroundDays = 90;
    public const int MaxSnomedCodeLength = 18;

    public Guid Id { get; init; }

    /// <summary>Unique.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Days from taken to reported the lab is expected to take (1 to 90). A specimen not reported by then is overdue at
    /// the lab: a date comparison, nothing clinical.
    /// </summary>
    public int ExpectedTurnaroundDays { get; set; }

    /// <summary>Its SNOMED CT specimen code (FHIR <c>Specimen.type</c>; MC13), optional: 6 to 18 digits, never looked up.</summary>
    public string? SnomedCode { get; set; }

    /// <summary>Archived types can't be chosen for new specimens; specimens already of the type keep it.</summary>
    public bool IsArchived { get; set; }

    public DateTime CreatedAt { get; init; }

    public DateTime LastUpdated { get; set; }
}
