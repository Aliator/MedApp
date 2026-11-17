using System.Globalization;
using MedApp.Domain.Notepads;

namespace MedApp.Application.Notepads.Common;

/// <summary>
/// The notepad's rules: one each, saved whole with the version the page last saw (a save from a stale tab is a 409), and
/// cleared once it has gone <see cref="NotepadOptions.KeepDays"/> without a change.
/// </summary>
public static class NotepadRules
{
    public const string ChangedMeanwhileMessage =
        "Your notepad was saved somewhere else (another tab or window) since this one loaded it. Reload it to see the latest, then make your change again.";

    /// <summary>Whether a notepad has gone <paramref name="keepDays"/> without a change at <paramref name="nowUtc"/>, so it's cleared.</summary>
    public static bool IsExpired(Notepad notepad, DateTime nowUtc, int keepDays) =>
        notepad.LastUpdatedUtc.AddDays(keepDays) <= nowUtc;

    /// <summary>The 409 when a save finds the notepad expired, and cleared.</summary>
    public static string ClearedMessage(int keepDays) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "Your notepad was cleared after {0} days without changes. Reload it to start again.",
            keepDays);
}
