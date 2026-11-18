using System.Net;
using MedApp.Application.Common.Email;
using MedApp.Application.PatientMessages.Common;
using MedApp.Domain.PatientMessages;
using MedApp.Infrastructure.Common.Email;
using Microsoft.Extensions.Options;

namespace MedApp.Infrastructure.PatientMessages;

/// <summary>
/// The offline stand-in for GOV.UK Notify (centre plan decision 77): every message is emailed to
/// <see cref="PatientMessageOptions.OfflineRecipient"/> through the staff email settings (Mailpit in development and on
/// kind), with its channel and real destination in the subject, and <b>never</b> to the patient. With no mail server
/// set, it's only marked as sent. Delivery is reported at once: delivered, except to the synthetic destinations that
/// always fail (<see cref="FailsFor"/>), so the failure path can be seen in a demonstration.
/// </summary>
public sealed class OfflinePatientMessageTransport(
    IEmailTransport email,
    IOptions<EmailDeliveryOptions> emailOptions,
    IOptions<PatientMessageOptions> options) : IPatientMessageTransport
{
    /// <summary>The reference prefix of a message that will fail; any other is delivered.</summary>
    private const string FailingPrefix = "offline-fail-";

    private const string DeliveredPrefix = "offline-";

    public PatientMessageMode Mode => PatientMessageMode.Offline;

    /// <summary>
    /// The synthetic destinations whose messages fail, as an invalid number or address would: a mobile ending 0666 (in
    /// Ofcom's drama range, 07700 900666) or an address whose name ends <c>+fail</c>.
    /// </summary>
    public static bool FailsFor(string destination) =>
        destination.EndsWith("0666", StringComparison.Ordinal)
        || destination.Split('@')[0].EndsWith("+fail", StringComparison.OrdinalIgnoreCase);

    public async Task<PatientMessageSendResult> SendAsync(PatientMessage message, CancellationToken ct)
    {
        // Checked at startup too: never a mailbox a real mail server would deliver to.
        if (!PatientMessageOptions.IsSafeOfflineRecipient(options.Value.OfflineRecipient))
            throw new InvalidOperationException(PatientMessageOptions.UnsafeOfflineRecipient);

        if (emailOptions.Value.CanSend)
        {
            var subject = $"[{message.Channel} to {message.Destination}] {PatientMessageTexts.Subject(message.Kind)}";
            var html = $"<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>{WebUtility.HtmlEncode(subject)}</title></head>"
                       + $"<body><p>{WebUtility.HtmlEncode(message.Body).Replace("\n", "<br>")}</p></body></html>";
            await email.SendAsync(new EmailMessage(options.Value.OfflineRecipient, subject, html, message.Body), ct);
        }

        var reference = (FailsFor(message.Destination) ? FailingPrefix : DeliveredPrefix) + message.Id.ToString("N");
        return PatientMessageSendResult.Accepted(reference);
    }

    public Task<PatientMessageDelivery> GetDeliveryAsync(string providerReference, CancellationToken ct) =>
        Task.FromResult(providerReference.StartsWith(FailingPrefix, StringComparison.Ordinal)
            ? PatientMessageDelivery.Failed("permanent-failure")
            : PatientMessageDelivery.Delivered);
}

/// <summary>Patient messages switched off: the job makes and sends nothing, so this is never asked.</summary>
public sealed class NoPatientMessageTransport : IPatientMessageTransport
{
    public PatientMessageMode Mode => PatientMessageMode.Off;

    public Task<PatientMessageSendResult> SendAsync(PatientMessage message, CancellationToken ct) =>
        throw new InvalidOperationException("Patient messages are off (PatientMessages:Mode).");

    public Task<PatientMessageDelivery> GetDeliveryAsync(string providerReference, CancellationToken ct) =>
        throw new InvalidOperationException("Patient messages are off (PatientMessages:Mode).");
}
