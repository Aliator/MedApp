namespace MedApp.Contracts.Procedures.Timeline.Responses;

/// <summary>A procedure the timeline can be filtered by.</summary>
public sealed class PatientTimelineProcedureResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>Deleted: its title is the one it had when it was deleted (admins only).</summary>
    public bool IsDeleted { get; set; }
}
