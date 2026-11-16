namespace MedApp.Contracts.Calendar.Responses;

public sealed class ActivityTypeResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>A palette colour, e.g. "green".</summary>
    public string Colour { get; set; } = string.Empty;

    /// <summary>Archived types can't be chosen for new blocks; existing blocks keep them.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Whether it can be deleted: true only if no block or template block has ever used it.</summary>
    public bool CanBeDeleted { get; set; }
}
