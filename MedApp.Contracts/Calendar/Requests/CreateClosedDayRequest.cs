namespace MedApp.Contracts.Calendar.Requests;

public sealed class CreateClosedDayRequest
{
    /// <summary>A UK date, today or later.</summary>
    public DateOnly Date { get; set; }

    /// <summary>At most 100 characters, e.g. "Staff training day". About the clinic, never a patient.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The site it closes (one that isn't archived), or null for everywhere. One everywhere per date, and one per site
    /// per date; a site can't be closed on a date that's already closed everywhere.
    /// </summary>
    public Guid? SiteId { get; set; }
}
