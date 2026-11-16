namespace MedApp.Domain.Calendar;

/// <summary>
/// One member of staff's named day of blocks, such as "Clinic day" with a lunch at 12:00, that their week templates and
/// day overrides apply to their calendar. Its blocks are dated, so an edit applies from the day it's made and past days
/// keep what they had.
/// </summary>
public sealed class DayTemplate
{
    public Guid Id { get; init; }

    /// <summary>
    /// The member of staff whose template it is (no foreign key to the users table, as with other staff IDs). Names are
    /// unique per owner, and only the owner's own calendar and templates use it.
    /// </summary>
    public Guid OwnerUserId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Archived day templates can't be chosen again, but keep applying where they already do. One that a week template
    /// or day override has ever referred to is archived, never deleted.
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>Every version of its blocks; the ones in force on a date are those valid on it.</summary>
    public List<DayTemplateBlock> Blocks { get; set; } = [];
}
