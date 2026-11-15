namespace MedApp.Contracts.Procedures.PatientProcedures.Responses;

public sealed class PatientProcedureStageResponse
{
    public Guid Id { get; set; }
    public Guid StageDefinitionId { get; set; }
    public int StageOrder { get; set; }

    /// <summary>Copied from the stage definition when the procedure was created.</summary>
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    /// <summary>NotStarted, InProgress or Completed.</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>The stage being worked on now.</summary>
    public bool IsCurrent { get; set; }

    /// <summary>A finished stage is locked until an admin unlocks it.</summary>
    public bool IsLocked { get; set; }

    /// <summary>Whether its notes can be edited and it can be finished now.</summary>
    public bool CanEdit { get; set; }

    /// <summary>Whether an admin can unlock it now: it's finished and locked, and the procedure isn't blocked, cancelled or archived.</summary>
    public bool CanUnlock { get; set; }

    public DateTime? StartedAtUtc { get; set; }
    public Guid? StartedByUserId { get; set; }
    public string? StartedByUsername { get; set; }

    public DateTime? CompletedAtUtc { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public string? CompletedByUsername { get; set; }
}
