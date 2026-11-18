using System.Globalization;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using MedApp.Domain.Referrals;
using static MedApp.Domain.Referrals.ReferralStatus;

namespace MedApp.Application.Referrals.Common;

/// <summary>
/// The rules of a referral's life: which statuses are open, what can be done to it in each, and the messages for what
/// can't. Handlers check here before changing a referral through <see cref="ReferralChange"/>.
/// <list type="bullet">
/// <item>Only <see cref="ReferralStatus.AwaitingTriage"/> is open. Its details can be edited, and it can be withdrawn or
/// marked entered in error; once it's closed, it never changes again.</item>
/// <item>A restricted or anonymised record's referrals can't be registered or edited (<see cref="PatientRules"/>). So
/// that care can be stopped safely, a restricted record's referral can still be withdrawn or marked entered in error,
/// as its pathways can be cancelled; an anonymised one's can't (anonymising waits until none awaits triage).</item>
/// </list>
/// </summary>
public static class ReferralWorkflow
{
    /// <summary>The statuses in which a referral is open: only awaiting triage.</summary>
    public static readonly IReadOnlyList<ReferralStatus> OpenStatuses = [AwaitingTriage];

    /// <summary>The statuses in which a referral is closed.</summary>
    public static readonly IReadOnlyList<ReferralStatus> ClosedStatuses =
        Enum.GetValues<ReferralStatus>().Where(s => s != AwaitingTriage).ToList();

    public const string ChangedMeanwhileMessage =
        "Someone else changed this referral while you had it open. Reload it to see their change, then try again.";

    /// <summary>
    /// A referral refused because the patient's record is restricted: the letter must not be lost (hazard H-40), so the
    /// message says what to do with it.
    /// </summary>
    public const string RestrictedRegisterMessage =
        "This patient's record is restricted, so a referral can't be registered for them until an admin lifts the restriction. " +
        "Give the referral letter to an admin now, so it isn't lost.";

    public const string AnonymisedRegisterMessage =
        "This patient's record has been anonymised, so a referral can't be registered for them. Check you have the right patient; " +
        "if you have, give the referral letter to an admin, so it isn't lost.";

    /// <summary>Why nobody can triage referrals yet: triage comes with the next release.</summary>
    public const string TriageNotAvailableMessage =
        "Triage isn't available yet. The referral waits here, in the Referrals inbox, until it is.";

    public const string EnteredInErrorFilesMessage =
        "Files can't be added to or removed from a referral that was entered in error.";

    public static bool IsOpen(ReferralStatus status) => status == AwaitingTriage;

    /// <summary>A status in words, for messages: "awaiting triage", "entered in error".</summary>
    public static string Describe(ReferralStatus status) => status switch
    {
        AwaitingTriage => "awaiting triage",
        Accepted => "accepted",
        Rejected => "rejected",
        RedirectedElsewhere => "redirected elsewhere",
        Withdrawn => "withdrawn",
        EnteredInError => "entered in error",
        _ => status.ToString()
    };

    public static string ClosedMessage(ReferralStatus status) =>
        $"This referral has been {Describe(status)}, so it can't be changed. Only a referral awaiting triage can be edited, withdrawn or marked entered in error.";

    /// <summary>A change must start from the version saved now, or someone else has changed it since (409).</summary>
    public static void EnsureVersion(Referral referral, int expectedVersion)
    {
        if (referral.Version != expectedVersion)
            throw new ConflictException(ChangedMeanwhileMessage);
    }

    /// <summary>Why the referral's details can't be edited now, or null if they can.</summary>
    public static string? WhyCantEdit(Referral referral) =>
        !IsOpen(referral.Status) ? ClosedMessage(referral.Status) : PatientRules.WhyCantChange(referral.Patient);

    /// <summary>
    /// Why the referral can't be withdrawn or marked entered in error now, or null if it can: it must await triage, and
    /// the record mustn't be anonymised (a restricted one's can, so care can be stopped safely).
    /// </summary>
    public static string? WhyCantClose(Referral referral) =>
        !IsOpen(referral.Status) ? ClosedMessage(referral.Status)
        : PatientRules.IsAnonymised(referral.Patient) ? PatientRules.AnonymisedMessage
        : null;

    /// <summary>Why files can't be attached to the referral, or deleted from it by staff, now; null if they can.</summary>
    public static string? WhyFilesCantChange(Referral referral) =>
        referral.Status == EnteredInError ? EnteredInErrorFilesMessage : null;

    /// <summary>Why the referral is read-only, for its page: the record's restriction or anonymisation, or its being closed.</summary>
    public static string? ReadOnlyReason(Referral referral) =>
        PatientRules.WhyCantChange(referral.Patient)
        ?? (IsOpen(referral.Status) ? null : $"This referral has been {Describe(referral.Status)}, so it can't be changed.");

    /// <summary>
    /// What a possible duplicate's confirmation says: the patient's open referrals to the same specialty, with when each
    /// was received and where it is (never what the letter said).
    /// </summary>
    public static string PossibleDuplicateMessage(string specialtyName, IReadOnlyList<Referral> open)
    {
        var each = open
            .OrderBy(r => r.ReceivedOn)
            .Select(r => $"received {r.ReceivedOn.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)} ({Describe(r.Status)})");
        var count = open.Count == 1 ? $"an open {specialtyName} referral" : $"{open.Count} open {specialtyName} referrals";
        return $"This patient already has {count}: {string.Join("; ", each)}. " +
               "If this is a different referral, confirm it to register it anyway.";
    }
}
