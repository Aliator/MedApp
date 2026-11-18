namespace MedApp.Contracts.Pathways.Timeline.Responses;

/// <summary>A pathway the timeline can be filtered by.</summary>
public sealed class PatientTimelinePathwayResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>Deleted: its title is the one it had when it was deleted (admins only).</summary>
    public bool IsDeleted { get; set; }

    /// <summary>The UK day it was created, to tell pathways with the same title apart; null once deleted.</summary>
    public DateOnly? CreatedOn { get; set; }

    /// <summary>The UK day it was deleted; null while it exists.</summary>
    public DateOnly? DeletedOn { get; set; }
}
