using System.Globalization;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Domain.Referrals;
using static MedApp.Domain.Referrals.ReferralStatus;

namespace MedApp.Application.Referrals.Common;

/// <summary>
/// The rules of a referral's life: which statuses are open, what can be done to it in each, and the messages for what
/// can't. Handlers check here before changing a referral through <see cref="ReferralChange"/>.
/// <list type="bullet">
/// <item><see cref="ReferralStatus.AwaitingTriage"/> and <see cref="ReferralStatus.InformationRequested"/> (more
/// information asked of the referrer, decision 106) are open. Their details can be edited, they can be triaged,
/// withdrawn or marked entered in error; once closed, a referral never changes again.</item>
/// <item>A restricted or anonymised record's referrals can't be registered or edited (<see cref="PatientRules"/>). So
/// that care can be stopped safely, a restricted record's referral can still be withdrawn or marked entered in error,
/// as its pathways can be cancelled; an anonymised one's can't (anonymising waits until none awaits triage).</item>
/// <item>Triage (accept, reject, redirect) needs the <c>TriageReferrals</c> permission and membership of the
/// referral's specialty (<see cref="WhyCantTriage"/>, a 403 otherwise), and a record that can change. Once triaged, a
/// referral isn't triaged again: a wrong acceptance is corrected by cancelling the pathway.</item>
/// </list>
/// </summary>
public static class ReferralWorkflow
{
    /// <summary>The statuses in which a referral is open: awaiting triage, or more information asked for.</summary>
    public static readonly IReadOnlyList<ReferralStatus> OpenStatuses = [AwaitingTriage, InformationRequested];

    /// <summary>The statuses in which a referral is closed.</summary>
    public static readonly IReadOnlyList<ReferralStatus> ClosedStatuses =
        Enum.GetValues<ReferralStatus>().Where(s => !OpenStatuses.Contains(s)).ToList();

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

    /// <summary>Why the viewer can't triage: their roles don't grant <c>TriageReferrals</c> (403).</summary>
    public const string NoTriagePermissionMessage =
        "Only clinicians who can triage referrals (their roles give them the Triage referrals permission) can accept, reject or redirect a referral.";

    /// <summary>Why the viewer can't triage: they hold the permission but don't belong to the referral's specialty (403).</summary>
    public static string NotInSpecialtyMessage(string specialtyName) =>
        $"Only members of {specialtyName} can triage this referral. If you should, ask an admin to add you to {specialtyName}.";

    public const string SameSpecialtyMessage = "The referral is already in that specialty. Choose another one.";

    public static string ArchivedSpecialtyMessage(string specialtyName) =>
        $"{specialtyName} is archived, so referrals can't be redirected to it.";

    public static string TemplateNotInSpecialtyMessage(string templateName, string specialtyName) =>
        $"The '{templateName}' template isn't one of {specialtyName}'s. To use another specialty's template, redirect the referral to that specialty first.";

    public const string EnteredInErrorFilesMessage =
        "Files can't be added to or removed from a referral that was entered in error.";

    public static bool IsOpen(ReferralStatus status) => status is AwaitingTriage or InformationRequested;

    public const string AlreadyAskedMessage =
        "More information has been asked for already. When it comes, record that it was received.";

    public const string NotAskedMessage = "No information was asked for on this referral, so there's nothing to record as received.";

    /// <summary>A status in words, for messages: "awaiting triage", "entered in error".</summary>
    public static string Describe(ReferralStatus status) => status switch
    {
        AwaitingTriage => "awaiting triage",
        InformationRequested => "waiting for more information",
        Accepted => "accepted",
        Rejected => "rejected",
        RedirectedElsewhere => "redirected elsewhere",
        Withdrawn => "withdrawn",
        EnteredInError => "entered in error",
        _ => status.ToString()
    };

    public static string ClosedMessage(ReferralStatus status) =>
        $"This referral has been {Describe(status)}, so it can't be changed. Only an open referral (awaiting triage, or waiting for more information) can be edited, withdrawn or marked entered in error.";

    /// <summary>
    /// What the second of two triagers is told: who decided, how and when (409). Also for a referral closed another way
    /// (withdrawn, entered in error) before the triager got to it.
    /// </summary>
    public static string AlreadyTriagedMessage(Referral referral)
    {
        var who = string.IsNullOrWhiteSpace(referral.DecidedByUsername) ? "Someone" : referral.DecidedByUsername;
        var how = referral.Status switch
        {
            Accepted => "accepted it into a pathway",
            Rejected => "rejected it",
            RedirectedElsewhere => "redirected it elsewhere",
            Withdrawn => "withdrew it",
            EnteredInError => "marked it entered in error",
            _ => $"marked it {Describe(referral.Status)}"
        };
        var when = referral.DecidedAtUtc is { } decided
            ? $" on {DueDates.ToUkDate(decided).ToString("d MMMM yyyy", CultureInfo.InvariantCulture)}"
            : string.Empty;
        return $"This referral can't be triaged now: {who} {how}{when}. Reload it to see the decision.";
    }

    /// <summary>
    /// Why <paramref name="viewer"/> can't triage the referral now, or null if they can: it must await triage, they must
    /// hold <c>TriageReferrals</c> and belong to its specialty (<paramref name="isMember"/>), and its patient's record
    /// mustn't be restricted or anonymised. Null for a closed referral (there's nothing to triage).
    /// </summary>
    public static string? WhyCantTriage(Referral referral, PathwayActor viewer, bool isMember) =>
        !IsOpen(referral.Status) ? null
        : !viewer.Has(Permission.TriageReferrals) ? NoTriagePermissionMessage
        : !isMember ? NotInSpecialtyMessage(referral.Specialty?.Name ?? "its specialty")
        : PatientRules.WhyCantChange(referral.Patient);

    /// <summary>
    /// Checks the actor may triage the referral now, in order: 403 without the permission or the membership; 409 once
    /// it's been decided (naming who and how), while the record is restricted or anonymised, or if it changed since
    /// <paramref name="expectedVersion"/>.
    /// </summary>
    public static void EnsureCanTriage(Referral referral, PathwayActor actor, bool isMember, int expectedVersion)
    {
        if (!actor.Has(Permission.TriageReferrals))
            throw new ForbiddenException(NoTriagePermissionMessage);
        if (!isMember)
            throw new ForbiddenException(NotInSpecialtyMessage(referral.Specialty?.Name ?? "its specialty"));
        if (!IsOpen(referral.Status))
            throw new ConflictException(AlreadyTriagedMessage(referral));
        PatientRules.EnsureCanChange(referral.Patient);
        EnsureVersion(referral, expectedVersion);
    }

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
