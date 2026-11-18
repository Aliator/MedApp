using MedApp.Application.Pathways.Common;
using MedApp.Contracts.PatientMessages.Responses;
using MedApp.Domain.PatientMessages;

namespace MedApp.Application.PatientMessages.Common;

public static class PatientMessageMapper
{
    /// <summary>A message's response, with the username of whoever asked for it (or "Deleted user").</summary>
    public static PatientMessageResponse ToResponse(PatientMessage message, IReadOnlyDictionary<Guid, string> usernames) => new()
    {
        Id = message.Id,
        AppointmentId = message.AppointmentId,
        Kind = message.Kind.ToString(),
        Channel = message.Channel.ToString(),
        Destination = message.Destination,
        Body = message.Body,
        Subject = message.Channel == PatientMessageChannel.Email ? PatientMessageTexts.Subject(message.Kind) : null,
        AppointmentStartUtc = message.AppointmentStartUtc,
        Status = message.Status.ToString(),
        StatusReason = message.StatusReason,
        RequestedByUsername = message.RequestedByUserId is { } id
            ? usernames.GetValueOrDefault(id) ?? PathwayMapper.DeletedUser
            : null,
        CreatedAtUtc = message.CreatedAtUtc,
        SentAtUtc = message.SentAtUtc,
        StatusAtUtc = message.StatusAtUtc,
    };
}
