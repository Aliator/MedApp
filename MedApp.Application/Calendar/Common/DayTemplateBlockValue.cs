using MedApp.Domain.Calendar;

namespace MedApp.Application.Calendar.Common;

/// <summary>
/// One block of a day template as it's asked for: an activity type, UK clock times and a note. Two blocks with the
/// same values are the same block, so an edit keeps the rows of blocks it didn't change (<see cref="TemplateHistory"/>).
/// </summary>
public sealed record DayTemplateBlockValue(Guid ActivityTypeId, TimeOnly StartTime, TimeOnly EndTime, string? Note)
{
    /// <summary>The note as stored: trimmed, empty when there's none.</summary>
    public string NoteText => Note?.Trim() ?? string.Empty;

    /// <summary>The value as compared and stored (the note normalised).</summary>
    public DayTemplateBlockValue Normalised => this with { Note = NoteText };

    public static DayTemplateBlockValue Of(DayTemplateBlock block) =>
        new(block.ActivityTypeId, block.StartTime, block.EndTime, block.Note);

    /// <summary>A new row of a day template for this value, valid from <paramref name="validFrom"/>.</summary>
    public DayTemplateBlock ToBlock(Guid dayTemplateId, DateOnly validFrom) => new()
    {
        Id = Guid.NewGuid(),
        DayTemplateId = dayTemplateId,
        ActivityTypeId = ActivityTypeId,
        StartTime = StartTime,
        EndTime = EndTime,
        Note = NoteText,
        ValidFrom = validFrom
    };
}
