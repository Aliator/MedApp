using MedApp.Domain.Letters;

namespace MedApp.Domain.Hospitals;

/// <summary>
/// A hospital that sends the lab samples with a request and gets its reports back (MC18 of
/// <c>docs/multi-clinic-plan.md</c>): organisation-wide setup, kept by holders of ManageSetup. One a request names is
/// archived, never deleted. Its name is admin-written, so audit entries may quote it.
/// </summary>
public sealed class Hospital
{
    public const int MaxNameLength = 200;
    public const int MinOdsCodeLength = 3;
    public const int MaxOdsCodeLength = 10;
    public const int MaxContactLength = 500;
    public const int MinDeadlineDays = 1;
    public const int MaxDeadlineDays = 90;

    public Guid Id { get; init; }

    /// <summary>Unique.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Its ODS code (3 to 10 capital letters and digits). Unique.</summary>
    public string OdsCode { get; set; } = string.Empty;

    /// <summary>Who to contact there, as the admin wrote it (a department, a phone number), if given.</summary>
    public string? Contact { get; set; }

    /// <summary>How its reports go (MC20): recorded as sent this way.</summary>
    public LetterDispatchMethod ReportDeliveryMethod { get; set; }

    /// <summary>Calendar days for its report when a request names no date (MC19's clock).</summary>
    public int DefaultDeadlineDays { get; set; }

    public bool IsArchived { get; set; }

    /// <summary>One more for each change.</summary>
    public int Version { get; set; }

    public DateTime CreatedAt { get; init; }

    public DateTime LastUpdated { get; set; }
}
