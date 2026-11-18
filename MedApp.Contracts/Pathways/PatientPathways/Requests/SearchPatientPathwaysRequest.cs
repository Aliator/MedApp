namespace MedApp.Contracts.Pathways.PatientPathways.Requests;

/// <summary>Query string for <c>GET api/pathways/patient-pathways/search</c>. Every filter is optional.</summary>
public sealed class SearchPatientPathwaysRequest
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    /// <summary>
    /// <c>dueDate</c> (the default), <c>priority</c>, <c>status</c>, <c>title</c>, <c>patient</c>, <c>lastUpdated</c> or
    /// <c>daysWaited</c> (the referral's waiting-time clock; pathways without one last).
    /// </summary>
    public string? Sort { get; set; }

    public bool Ascending { get; set; } = true;

    /// <summary>Matches the title, template name or patient name, ignoring case.</summary>
    public string? Search { get; set; }

    /// <summary>The title contains this, ignoring case.</summary>
    public string? Title { get; set; }

    /// <summary>The template name contains this, ignoring case.</summary>
    public string? TemplateName { get; set; }

    /// <summary>The patient's full name contains this, ignoring case.</summary>
    public string? PatientName { get; set; }

    public Guid? PatientId { get; set; }

    public Guid? TemplateId { get; set; }

    /// <summary>In any of these specialties (the pathway's own, copied from its template when it was made).</summary>
    public List<Guid>? SpecialtyIds { get; set; }

    /// <summary>
    /// The stage reached: pathways whose current stage (the first unfinished one) comes from any of these stage
    /// definitions. Discharged, cancelled and archived pathways never match.
    /// </summary>
    public List<Guid>? StageDefinitionIds { get; set; }

    /// <summary>Any of these statuses. When set, it overrides <see cref="IncludeClosed"/>.</summary>
    public List<string>? Statuses { get; set; }

    public string? Priority { get; set; }

    /// <summary>Any of these priorities (with <see cref="Priority"/>, if both are set).</summary>
    public List<string>? Priorities { get; set; }

    public Guid? AssignedToUserId { get; set; }

    /// <summary>Only pathways assigned to the signed-in user.</summary>
    public bool AssignedToMe { get; set; }

    /// <summary>Only pathways nobody is assigned.</summary>
    public bool NobodyAssigned { get; set; }

    public bool OverdueOnly { get; set; }

    /// <summary>Only pathways whose referral's waiting-time clock is past its target.</summary>
    public bool BreachedOnly { get; set; }

    public DateOnly? DueFrom { get; set; }

    public DateOnly? DueTo { get; set; }

    /// <summary>Include Completed, Cancelled and Archived pathways (hidden by default).</summary>
    public bool IncludeClosed { get; set; }

    /// <summary>True: only pathways where work has begun on a stage. False: only those where none has. Unset: both.</summary>
    public bool? Started { get; set; }
}
