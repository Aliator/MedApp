using System.Text.Json;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Contracts.Referrals.Responses;
using MedApp.Domain.Referrals;

namespace MedApp.Application.Referrals.Common;

/// <summary>Referrals as the API returns them: a referral's page, a row in a list, and its history.</summary>
public static class ReferralMapper
{
    /// <summary>
    /// A referral's page: the patient for the banner, its details, history and what <paramref name="viewer"/> may do.
    /// The referral is loaded with its patient and specialty. <paramref name="viewerIsMember"/> says whether the viewer
    /// belongs to its specialty (triage needs it, with the permission).
    /// </summary>
    public static ReferralResponse ToResponse(
        Referral referral,
        IReadOnlyList<ReferralEvent> history,
        int letterCount,
        PathwayActor viewer,
        bool viewerIsMember = false)
    {
        var canRegister = viewer.Has(Permission.RegisterReferrals);
        var whyCantTriage = ReferralWorkflow.WhyCantTriage(referral, viewer, viewerIsMember);
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
            PracticeId = referral.PracticeId,
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
            RedirectDetail = referral.RedirectDetail,
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
            CanTriage = ReferralWorkflow.IsOpen(referral.Status) && whyCantTriage is null,
            WhyCantTriage = whyCantTriage,
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

    /// <summary>The referral a pathway was accepted from, for the pathway's page (loaded with its specialty).</summary>
    public static PathwayReferralResponse ToPathwayReferral(Referral referral) => new()
    {
        Id = referral.Id,
        ReceivedOn = referral.ReceivedOn,
        SpecialtyName = referral.Specialty?.Name ?? string.Empty
    };

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
            Priority = details.Priority,
            TemplateName = details.TemplateName,
            PathwayId = details.PathwayId,
            RejectionReason = details.RejectionReason,
            ChangedFields = details.ChangedFields.ToList()
        };
    }
}

/// <summary>
/// What a referral history entry's details say, read from the JSON <see cref="ReferralChange"/> writes: the status
/// after it, the specialty (and the one before, for an edit or a redirection that moved it), the priority asked for,
/// the fields an edit changed with the structured ones' values before and after, and for triage the priority and
/// template chosen, the pathway and the rejection reason.
/// </summary>
public sealed record ReferralEventDetails(
    string? ToStatus,
    string? SpecialtyName,
    string? PreviousSpecialtyName,
    string? RequestedPriority,
    IReadOnlyList<string> ChangedFields,
    IReadOnlyDictionary<string, (string? From, string? To)> Changes)
{
    /// <summary>Accepted: the pathway's priority, as the triager chose it.</summary>
    public string? Priority { get; init; }

    /// <summary>Accepted: the template the pathway was made from.</summary>
    public string? TemplateName { get; init; }

    /// <summary>Accepted: the pathway.</summary>
    public Guid? PathwayId { get; init; }

    /// <summary>Rejected: why, by name.</summary>
    public string? RejectionReason { get; init; }

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
            PreviousSpecialtyName: Text(root, "previousSpecialtyName") ?? specialty.From,
            RequestedPriority: Text(root, "requestedPriority"),
            ChangedFields: fields,
            Changes: changes)
        {
            Priority = Text(root, "priority"),
            TemplateName = Text(root, "templateName"),
            PathwayId = Guid.TryParse(Text(root, "pathwayId"), out var pathwayId) ? pathwayId : null,
            RejectionReason = Text(root, "rejectionReason")
        };
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
