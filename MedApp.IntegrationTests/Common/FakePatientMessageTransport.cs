using System.Collections.Concurrent;
using MedApp.Application.PatientMessages.Common;
using MedApp.Domain.PatientMessages;

namespace MedApp.IntegrationTests.Common;

/// <summary>
/// The API's patient message sender in the integration tests (patient messages are Offline there): it keeps every
/// message handed to it, accepts it, and reports it delivered. Emptied before each test.
/// </summary>
public sealed class FakePatientMessageTransport : IPatientMessageTransport
{
    private readonly ConcurrentQueue<PatientMessage> _sent = new();

    public PatientMessageMode Mode => PatientMessageMode.Offline;

    public IReadOnlyList<PatientMessage> Sent => _sent.ToList();

    public Task<PatientMessageSendResult> SendAsync(PatientMessage message, CancellationToken ct)
    {
        _sent.Enqueue(message);
        return Task.FromResult(PatientMessageSendResult.Accepted("fake-" + message.Id.ToString("N")));
    }

    public Task<PatientMessageDelivery> GetDeliveryAsync(string providerReference, CancellationToken ct) =>
        Task.FromResult(PatientMessageDelivery.Delivered);

    public void Clear() => _sent.Clear();
}
