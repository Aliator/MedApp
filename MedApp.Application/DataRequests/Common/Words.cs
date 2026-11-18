using System.Globalization;
using System.Text;
using MedApp.Application.Calendar.Common;

namespace MedApp.Application.DataRequests.Common;

/// <summary>Stored names and times in plain words, for the pack's report.</summary>
public static class Words
{
    private static readonly CultureInfo British = CultureInfo.GetCultureInfo("en-GB");

    /// <summary>"InProgress" as "In progress", "GpPractice" as "Gp practice": a code name in words.</summary>
    public static string Humanise(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        var words = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
                words.Append(' ').Append(char.ToLowerInvariant(c));
            else
                words.Append(c);
        }

        return words.ToString();
    }

    /// <summary>A pathway's status as staff see it: Blocked is "On hold", Completed is "Discharged" (the glossary).</summary>
    public static string PathwayStatus(string status) => status switch
    {
        "Blocked" => "On hold",
        "Completed" => "Discharged",
        _ => Humanise(status)
    };

    public static string Date(DateOnly date) => date.ToString("d MMMM yyyy", British);

    public static string Date(DateOnly? date) => date is { } d ? Date(d) : string.Empty;

    /// <summary>A UTC time as the UK date and time, e.g. "3 March 2026, 14:05".</summary>
    public static string Time(DateTime utc) => UkTime.ToUk(utc).ToString("d MMMM yyyy, HH:mm", British);

    public static string Time(DateTime? utc) => utc is { } u ? Time(u) : string.Empty;

    /// <summary>A file's size in words: "512 bytes", "34 KB", "2.1 MB".</summary>
    public static string Size(long bytes) =>
        bytes < 1024 ? $"{bytes} bytes"
        : bytes < 1024 * 1024 ? string.Create(British, $"{bytes / 1024.0:0} KB")
        : string.Create(British, $"{bytes / (1024.0 * 1024.0):0.0} MB");
}
