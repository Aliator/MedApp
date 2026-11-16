using System.Buffers.Text;
using System.Globalization;
using System.Text;
using MedApp.Domain.Appointments;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.Timeline.Common;

/// <summary>
/// Where a page of the timeline ended: its last entry's time and ID (the timeline's order), so the next page starts
/// just after it however many entries have been added since. Clients get it as an opaque string.
/// </summary>
public sealed record TimelineCursor(DateTime AtUtc, Guid Id)
{
    private const int MaxLength = 100;

    /// <summary>The cursor for the page after this entry.</summary>
    public static TimelineCursor After(PatientProcedureAuditEntry entry) => new(entry.AtUtc, entry.Id);

    /// <summary>The cursor for the page after this appointment history entry.</summary>
    public static TimelineCursor After(AppointmentEvent entry) => new(entry.AtUtc, entry.Id);

    public string Encode() =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes($"{AtUtc.Ticks.ToString(CultureInfo.InvariantCulture)}.{Id:N}"));

    /// <summary>Reads a cursor that <see cref="Encode"/> wrote; false for anything else.</summary>
    public static bool TryDecode(string? value, out TimelineCursor cursor)
    {
        cursor = null!;
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxLength)
            return false;

        string text;
        try
        {
            text = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(value));
        }
        catch (FormatException)
        {
            return false;
        }

        var parts = text.Split('.');
        if (parts.Length != 2
            || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
            || ticks > DateTime.MaxValue.Ticks
            || !Guid.TryParseExact(parts[1], "N", out var id))
            return false;

        cursor = new TimelineCursor(new DateTime(ticks, DateTimeKind.Utc), id);
        return true;
    }
}
