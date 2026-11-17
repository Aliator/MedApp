using MedApp.Domain.Notepads;

namespace MedApp.UnitTests.Common.Constants;

public static class NotepadTestConstants
{
    /// <summary>"Now" in notepad tests.</summary>
    public static readonly DateTime Now = new(2026, 11, 17, 10, 32, 0, DateTimeKind.Utc);

    public static readonly Guid UserId = Guid.Parse("0000000c-0000-0000-0000-000000000001");

    public const int KeepDays = 30;

    /// <summary>Synthetic jottings: no patient details.</summary>
    public const string Text = "Synthetic jotting: call the ward back after 2pm.";

    /// <summary>A notepad last saved <paramref name="daysAgo"/> days before <see cref="Now"/>.</summary>
    public static Notepad CreateNotepad(double daysAgo = 1, int version = 3, string text = Text) => new()
    {
        UserId = UserId,
        Text = text,
        LastUpdatedUtc = Now.AddDays(-daysAgo),
        Version = version
    };
}
