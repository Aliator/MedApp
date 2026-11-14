namespace MedApp.Contracts.Procedures.PatientProcedures.Requests;

/// <summary>Query string for <c>GET api/procedures/patient-procedures/search</c>. Every filter is optional.</summary>
public sealed class SearchPatientProceduresRequest
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    /// <summary><c>dueDate</c> (the default), <c>priority</c>, <c>status</c>, <c>title</c>, <c>patient</c> or <c>lastUpdated</c>.</summary>
    public string? Sort { get; set; }

    public bool Ascending { get; set; } = true;

    /// <summary>Matches the title, template name or patient name, ignoring case.</summary>
    public string? Search { get; set; }

    public Guid? PatientId { get; set; }

    public Guid? TemplateId { get; set; }

    /// <summary>Any of these statuses. When set, it overrides <see cref="IncludeClosed"/>.</summary>
    public List<string>? Statuses { get; set; }

    public string? Priority { get; set; }

    public Guid? AssignedToUserId { get; set; }

    /// <summary>Only procedures assigned to the signed-in user.</summary>
    public bool AssignedToMe { get; set; }

    public bool OverdueOnly { get; set; }

    public DateOnly? DueFrom { get; set; }

    public DateOnly? DueTo { get; set; }

    /// <summary>Include Completed, Cancelled and Archived procedures (hidden by default).</summary>
    public bool IncludeClosed { get; set; }

    /// <summary>True: only procedures where work has begun on a stage. False: only those where none has. Unset: both.</summary>
    public bool? Started { get; set; }
}
