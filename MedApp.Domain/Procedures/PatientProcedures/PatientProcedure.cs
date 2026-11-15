using MedApp.Domain.Patients;

namespace MedApp.Domain.Procedures.PatientProcedures;

/// <summary>
/// One patient going through a procedure template: a copy of the template's stages, worked through in order.
/// </summary>
public sealed class PatientProcedure
{
    public Guid Id { get; init; }

    public string Title { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public DateTime DueDateUtc { get; set; }

    public PatientProcedurePriority Priority { get; set; }

    public PatientProcedureStatus Status { get; set; }

    /// <summary>The status a Blocked procedure goes back to when it's unblocked.</summary>
    public PatientProcedureStatus? BlockedFromStatus { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    /// <summary>The template it was created from (null for procedures created before templates were required).</summary>
    public Guid? TemplateId { get; set; }

    /// <summary>The template's name when the procedure was created, kept even if the template is renamed or archived.</summary>
    public string TemplateName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; init; }
    public DateTime LastUpdated { get; set; }

    public List<PatientProcedureStage> Stages { get; set; } = [];
    public List<PatientProcedureAssignment> Assignments { get; set; } = [];
}
