namespace MedApp.Domain.Procedures.PatientProcedures;

/// <summary>
/// A standard procedure: an ordered list of stages that staff create a patient's procedure from. Only admins edit
/// templates; a procedure copies its template's stages when it's created, so later edits don't change it.
/// </summary>
public sealed class PatientProcedureStageTemplate
{
    public Guid Id { get; init; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Archived templates can't be used for new procedures. A template in use is archived, never deleted.</summary>
    public bool IsArchived { get; set; }

    public List<PatientProcedureStageTemplateMap> Maps { get; set; } = [];
}
