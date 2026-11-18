using System.Globalization;
using MedApp.Application.Calendar.Common;
using MedApp.Domain.PatientMessages;

namespace MedApp.Application.PatientMessages.Common;

/// <summary>
/// The fixed wording of every message to a patient (centre plan decision 131). <b>A message never says why:</b> it holds
/// the organisation's name, the date and time, the site's name, address and phone, and a fixed phrase. Never the
/// specialty, clinician, pathway, stage, any note or the NHS number. A text is kept to 160 characters where it can be:
/// the address goes first, then the postcode, if it's longer.
/// </summary>
public static class PatientMessageTexts
{
    /// <summary>The most a text should be: one SMS.</summary>
    public const int TextLength = 160;

    /// <summary>The line every email ends with: Notify and the offline sender don't read replies.</summary>
    public const string EmailFooter = "This is an automated message. Replies to it aren't read.";

    /// <summary>An email's subject, a fixed phrase for each kind.</summary>
    public static string Subject(PatientMessageKind kind) => kind switch
    {
        PatientMessageKind.AppointmentReminder => "Appointment reminder",
        PatientMessageKind.AppointmentMoved => "Your appointment has moved",
        PatientMessageKind.AppointmentCancelled => "Your appointment has been cancelled",
        PatientMessageKind.MissedAppointment => "We missed you at your appointment",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    /// <summary>
    /// The text of a message of this kind about an appointment starting at <paramref name="startUtc"/> (shown in UK
    /// time), at <paramref name="site"/> if it's known, from <paramref name="sender"/>. <paramref name="contactPhone"/> is
    /// asked for when the site has no phone.
    /// </summary>
    public static string Body(
        PatientMessageKind kind,
        PatientMessageChannel channel,
        string sender,
        DateTime startUtc,
        MessageSite? site,
        string? contactPhone)
    {
        var phone = !string.IsNullOrWhiteSpace(site?.Phone) ? site.Phone.Trim()
            : !string.IsNullOrWhiteSpace(contactPhone) ? contactPhone.Trim()
            : null;

        // From the fullest to the shortest: a text takes the first that fits; an email always the fullest.
        var places = Places(site).ToList();
        string text = string.Empty;
        foreach (var place in places)
        {
            text = Compose(kind, sender.Trim(), startUtc, place, phone);
            if (channel == PatientMessageChannel.Email || text.Length <= TextLength)
                break;
        }

        return channel == PatientMessageChannel.Email ? text + "\n\n" + EmailFooter : text;
    }

    private static string Compose(PatientMessageKind kind, string sender, DateTime startUtc, string place, string? phone)
    {
        var when = When(startUtc);
        var change = phone is null ? "To change it, please contact us." : $"To change it, call {phone}.";
        var another = phone is null ? "Please contact us to arrange another." : $"Please call {phone} to arrange another.";

        return kind switch
        {
            PatientMessageKind.AppointmentReminder => $"{sender}: reminder of your appointment on {when}{place}. {change}",
            PatientMessageKind.AppointmentMoved => $"{sender}: your appointment has moved to {when}{place}. {change}",
            PatientMessageKind.AppointmentCancelled => $"{sender}: your appointment on {when}{place} has been cancelled. {another}",
            PatientMessageKind.MissedAppointment => $"{sender}: we missed you at your appointment on {when}{place}. {another}",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    /// <summary>"Tue 3 Mar at 10:30", in UK time.</summary>
    public static string When(DateTime startUtc)
    {
        var uk = UkTime.ToUk(startUtc);
        return uk.ToString("ddd d MMM", CultureInfo.InvariantCulture) + " at " + uk.ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>" at Main site, 1 High Street, ZZ1 1AA", then without the address lines, then without the postcode; none without a site.</summary>
    private static IEnumerable<string> Places(MessageSite? site)
    {
        if (site is null)
        {
            yield return string.Empty;
            yield break;
        }

        var postcode = string.IsNullOrWhiteSpace(site.Postcode) ? null : site.Postcode.Trim();
        var lines = site.AddressLines.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()).ToList();
        if (lines.Count > 0)
            yield return " at " + string.Join(", ", [site.Name, .. lines, .. postcode is null ? Array.Empty<string>() : [postcode]]);
        if (postcode is not null)
            yield return $" at {site.Name}, {postcode}";
        yield return $" at {site.Name}";
    }
}
