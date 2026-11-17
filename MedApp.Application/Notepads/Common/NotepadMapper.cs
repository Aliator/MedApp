using MedApp.Contracts.Notepads.Responses;
using MedApp.Domain.Notepads;

namespace MedApp.Application.Notepads.Common;

public static class NotepadMapper
{
    /// <summary>A notepad, or an empty one (version 0) when there's none.</summary>
    public static NotepadResponse ToResponse(Notepad? notepad, int keepDays) => new()
    {
        Text = notepad?.Text ?? string.Empty,
        LastUpdatedUtc = notepad?.LastUpdatedUtc,
        Version = notepad?.Version ?? 0,
        KeepDays = keepDays
    };
}
