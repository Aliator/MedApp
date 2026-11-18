using System.Text.Json;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Referrals.Responses;
using MedApp.Domain.Referrals;

namespace MedApp.Application.Referrals.Common;

/// <summary>Referrals as the API returns them: a referral's page, a row in a list, and its history.</summary>
public static class ReferralMapper
{
    /// <summary>
    /// A referral's page: the patient for the banner, its details, history and what <paramref name="viewer"/> may do.
    /// The referral is loaded with its patient and specialty.
    /// </summary>
    public static ReferralResponse ToResponse(
        Referral referral,
        IReadOnlyList<ReferralEvent> history,
        int letterCount,
        PathwayActor viewer)
    {
        var canRegister = viewer.Has(Permission.RegisterReferrals);
        return new ReferralResponse
        {
            Id = referral.Id,
            PatientId = referral.PatientId,
            Patient = PathwayMapper.ToPatient(referral.Patient),
            PatientFlags = referral.Patient is null ? null : PatientMapper.ToFlags(referral.Patient),
            SpecialtyId = referral.SpecialtyId,
            SpecialtyName = referral.Specialty?.Name ?? string.Empty,
            ReceivedOn = referral.ReceivedOn,
            ReferrerPractice = referral.ReferrerPractice,
            ReferrerClinician = referral.ReferrerClinician,
            ReferrerOdsCode = referral.ReferrerOdsCode,
            RequestedPriority = referral.RequestedPriority.ToString(),
            Reason = referral.Reason,
            Status = referral.Status.ToString(),
            IsOpen = ReferralWorkflow.IsOpen(referral.Status),
            RegisteredAtUtc = referral.RegisteredAtUtc,
            RegisteredByUsername = referral.RegisteredByUsername,
            DecidedAtUtc = referral.DecidedAtUtc,
            DecidedByUsername = referral.DecidedByUsername,
            RejectionReason = referral.RejectionReason?.ToString(),
            DecisionDetail = referral.DecisionDetail,
            RedirectedTo = referral.RedirectedTo,
            PathwayId = referral.PathwayId,
            Version = referral.Version,
            LastUpdated = referral.LastUpdated,
            LetterCount = letterCount,
            CanEdit = canRegister && ReferralWorkflow.WhyCantEdit(referral) is null,
            CanClose = canRegister && ReferralWorkflow.WhyCantClose(referral) is null,
            CanAttachFiles = viewer.Has(Permission.AttachFiles)
                             && PatientRules.CanChange(referral.Patient)
                             && ReferralWorkflow.WhyFilesCantChange(referral) is null,
            ReadOnlyReason = ReferralWorkflow.ReadOnlyReason(referral),
            CanTriage = false,
            WhyCantTriage = ReferralWorkflow.IsOpen(referral.Status) ? ReferralWorkflow.TriageNotAvailableMessage : null,
            History = history.OrderBy(e => e.AtUtc).ThenBy(e => e.Id).Select(ToResponse).ToList()
        };
    }

    /// <summary>
    /// A row in a list, with its patient and specialty, and the whole days since it was received: up to
    /// <paramref name="today"/> (UK) while it's open, or to the day it was decided.
    /// </summary>
    public static ReferralSummaryResponse ToSummary(ReferralRow row, DateOnly today)
    {
        var referral = row.Referral;
        var until = referral.DecidedAtUtc is { } decided ? DueDates.ToUkDate(decided) : today;
        return new ReferralSummaryResponse
        {
            Id = referral.Id,
            PatientId = referral.PatientId,
            PatientName = referral.Patient is null ? string.Empty : PatientMapper.FullName(referral.Patient),
            PatientNhsNumber = referral.Patient?.NhsNumber,
            PatientDateOfBirth = referral.Patient?.DateOfBirth ?? default,
            SpecialtyId = referral.SpecialtyId,
            SpecialtyName = referral.Specialty?.Name ?? string.Empty,
            ReceivedOn = referral.ReceivedOn,
            DaysSinceReceived = Math.Max(0, until.DayNumber - referral.ReceivedOn.DayNumber),
            RequestedPriority = referral.RequestedPriority.ToString(),
            ReferrerPractice = referral.ReferrerPractice,
            Status = referral.Status.ToString(),
            HasLetter = row.HasLetter,
            RegisteredAtUtc = referral.RegisteredAtUtc,
            DecidedAtUtc = referral.DecidedAtUtc
        };
    }

    /// <summary>One history entry, read from its structured details.</summary>
    public static ReferralEventResponse ToResponse(ReferralEvent entry)
    {
        var details = ReferralEventDetails.Parse(entry.Details);
        return new ReferralEventResponse
        {
            Id = entry.Id,
            Action = entry.Action.ToString(),
            AtUtc = entry.AtUtc,
            Username = entry.Username,
            ToStatus = details.ToStatus,
            SpecialtyName = details.SpecialtyName,
            PreviousSpecialtyName = details.PreviousSpecialtyName,
            RequestedPriority = details.RequestedPriority,
            ChangedFields = details.ChangedFields.ToList()
        };
    }
}

/// <summary>
/// What a referral history entry's details say, read from the JSON <see cref="ReferralChange"/> writes: the status
/// after it, the specialty (and the one before, for an edit that moved it), the priority asked for, and the fields an
/// edit changed with the structured ones' values before and after.
/// </summary>
public sealed record ReferralEventDetails(
    string? ToStatus,
    string? SpecialtyName,
    string? PreviousSpecialtyName,
    string? RequestedPriority,
    IReadOnlyList<string> ChangedFields,
    IReadOnlyDictionary<string, (string? From, string? To)> Changes)
{
    public static ReferralEventDetails Parse(string json)
    {
        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(json);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            root = default;
        }

        if (root.ValueKind != JsonValueKind.Object)
            return new ReferralEventDetails(null, null, null, null, [], new Dictionary<string, (string?, string?)>());

        var fields = root.TryGetProperty("fields", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Where(f => f.ValueKind == JsonValueKind.String).Select(f => f.GetString()!).ToList()
            : [];

        var changes = new Dictionary<string, (string? From, string? To)>();
        if (root.TryGetProperty("changes", out var changed) && changed.ValueKind == JsonValueKind.Object)
            foreach (var change in changed.EnumerateObject().Where(c => c.Value.ValueKind == JsonValueKind.Object))
                changes[change.Name] = (Text(change.Value, "from"), Text(change.Value, "to"));

        var specialty = changes.GetValueOrDefault(ReferralChange.SpecialtyField);
        return new ReferralEventDetails(
            ToStatus: Text(root, "to") ?? Text(root, "status"),
            SpecialtyName: Text(root, "specialtyName") ?? specialty.To,
            PreviousSpecialtyName: specialty.From,
            RequestedPriority: Text(root, "requestedPriority"),
            ChangedFields: fields,
            Changes: changes);
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
