namespace MedApp.Domain.Procedures.PatientProcedures;

/// <summary>A reusable stage, such as "Consultation", that templates list (possibly more than once).</summary>
public sealed class PatientProcedureStageDefinition
{
    public Guid Id { get; init; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Instructions { get; set; } = string.Empty;

    /// <summary>
    /// The SNOMED CT concept the stage records (its SCTID), or null. Typed by an admin and checked for format only
    /// (<see cref="Terminology.SnomedCt"/>): there's no terminology server.
    /// </summary>
    public string? SnomedCode { get; set; }

    /// <summary>The concept's display term, as the admin typed it; set exactly when <see cref="SnomedCode"/> is.</summary>
    public string? SnomedTerm { get; set; }
    /// <summary>Whether the stage involves a visit; copied onto each procedure's stage when it's created.</summary>
    public VisitRequirement VisitRequirement { get; set; }

    /// <summary>Archived definitions can't be added to templates. A definition in use is archived, never deleted.</summary>
    public bool IsArchived { get; set; }
}
