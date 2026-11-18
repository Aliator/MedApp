using System.Globalization;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Domain.Letters;
using MedApp.Domain.Patients;
using static MedApp.Domain.Letters.LetterStatus;

namespace MedApp.Application.Letters.Common;

/// <summary>
/// The rules of a letter's life (decisions 100 to 105 of docs/centre-phases-3-5-plan.md), and the messages for what
/// can't be done. Handlers check here before changing a letter through <see cref="LetterChange"/>.
/// <list type="bullet">
/// <item><b>A draft is its author's alone:</b> only they edit, submit or discard it. If they hold ApproveLetters they may
/// approve it themselves; otherwise they submit it to a named holder.</item>
/// <item><b>Awaiting approval,</b> only the named approver approves or returns it; returning makes it a draft again, with
/// the approver's comment.</item>
/// <item><b>Approved,</b> it's frozen: only sending it, and withdrawing it (its approver, or someone who can correct
/// records), change it. A withdrawn letter is final; a correction is a new draft.</item>
/// <item><b>Restricted or anonymised records</b> can't have letters started, changed, approved or sent (409, asking an
/// admin, decision 75). Withdrawing an approved letter protects the patient, so a restricted record's still can be
/// withdrawn; an anonymised one's can't.</item>
/// </list>
/// </summary>
public static class LetterWorkflow
{
    /// <summary>How far back a letter's sending may be recorded, in days.</summary>
    public const int MaxDaysBack = 7;

    public const string ChangedMeanwhileMessage =
        "Someone changed this letter while you had it open. Reload it to see the change, then try again.";

    public const string NotAuthorMessage = "Only the letter's author can change it while it's a draft.";

    public const string NotApproverMessage =
        "Only the person it was submitted to can approve or return this letter. Ask them, or ask its author to submit it to you.";

    public const string CantApproveOwnMessage =
        "You can't approve letters (your roles don't give you the Approve letters permission). Submit it to someone who can.";

    public const string SubmitToSelfMessage = "Submit the letter to someone else, or approve it yourself if you can approve letters.";

    public const string NotWithdrawerMessage =
        "Only the person who approved this letter, or someone who can correct records, can withdraw it.";

    public const string RestrictedMessage =
        "This patient's record is restricted, so their letters can't be written, approved or sent until an admin lifts the restriction. " +
        "Ask an admin about this letter.";

    public const string NoFullNameMessage =
        "Add your full name in Account Settings before approving letters: it's printed on every letter you approve.";

    public const string NoRecipientsMessage = "Add at least one recipient before the letter is approved.";

    public const string SecureEmailOffMessage =
        "Letters can't be sent by secure email from MedApp here: it isn't set up, or the organisation hasn't declared its relay meets the secure email standard. " +
        "Send it another way and record that.";

    public const string PatientEmailMessage =
        "Letters aren't emailed to patients: their address isn't verified and the letter would travel unencrypted. Post it or hand it over.";

    public const string NoSecureEmailAddressMessage =
        "This recipient has no secure email address. Add one to the practice in the directory, or send the letter another way.";

    /// <summary>The kinds a letter written from a referral's page may be (decision 106).</summary>
    public static readonly IReadOnlySet<LetterKind> ReferralKinds =
        new HashSet<LetterKind> { LetterKind.ReferralResponse, LetterKind.InformationRequest };

    /// <summary>The methods staff record a sending with (secure email from MedApp is sent, not recorded).</summary>
    public static readonly IReadOnlySet<LetterDispatchMethod> RecordedMethods = new HashSet<LetterDispatchMethod>
    {
        LetterDispatchMethod.Post,
        LetterDispatchMethod.HandedToPatient,
        LetterDispatchMethod.EmailOutsideMedApp,
        LetterDispatchMethod.Other,
    };

    /// <summary>A status in words, for messages.</summary>
    public static string Describe(LetterStatus status) => status switch
    {
        Draft => "a draft",
        AwaitingApproval => "awaiting approval",
        Approved => "approved",
        Withdrawn => "withdrawn",
        _ => status.ToString()
    };

    public static string NotInStatusMessage(Letter letter, string action) =>
        $"This letter is {Describe(letter.Status)}, so it can't be {action}. Reload it to see where it is.";

    /// <summary>A change must start from the version saved now, or someone else has changed it since (409).</summary>
    public static void EnsureVersion(Letter letter, int expectedVersion)
    {
        if (letter.Version != expectedVersion)
            throw new ConflictException(ChangedMeanwhileMessage);
    }

    /// <summary>Why the patient's record stops letters, or null: the letters' own message for a restricted record.</summary>
    public static string? WhyRecordStopsLetters(Patient? patient) =>
        PatientRules.IsAnonymised(patient) ? PatientRules.AnonymisedMessage
        : PatientRules.IsRestricted(patient) ? RestrictedMessage
        : null;

    /// <summary>Throws a 409 if the record stops letters (restricted or anonymised).</summary>
    public static void EnsureRecordAllowsLetters(Patient? patient)
    {
        if (WhyRecordStopsLetters(patient) is { } reason)
            throw new ConflictException(reason);
    }

    public static bool IsReturned(Letter letter) => letter.Status == Draft && letter.ReturnComment is not null;

    /// <summary>Its author, working on their draft (edit, submit, discard).</summary>
    public static bool IsAuthorsDraft(Letter letter, PathwayActor viewer) =>
        letter.Status == Draft && letter.AuthorUserId == viewer.UserId && viewer.Has(Permission.WriteLetters);

    /// <summary>Whether the viewer may approve it now: its named approver, or its author approving their own draft.</summary>
    public static bool CanApprove(Letter letter, PathwayActor viewer) =>
        viewer.Has(Permission.ApproveLetters)
        && (letter.Status == AwaitingApproval
            ? letter.ApproverUserId == viewer.UserId
            : letter.Status == Draft && letter.AuthorUserId == viewer.UserId);

    /// <summary>Whether the viewer may return it: its named approver, while it awaits approval.</summary>
    public static bool CanReturn(Letter letter, PathwayActor viewer) =>
        letter.Status == AwaitingApproval && letter.ApproverUserId == viewer.UserId && viewer.Has(Permission.ApproveLetters);

    /// <summary>Whether the viewer may withdraw it: approved, and they approved it or can correct records.</summary>
    public static bool CanWithdraw(Letter letter, PathwayActor viewer) =>
        letter.Status == Approved
        && ((letter.ApproverUserId == viewer.UserId && viewer.Has(Permission.ApproveLetters)) || viewer.Has(Permission.CorrectRecords));

    /// <summary>Whether the viewer may see its draft's preview: its author or approver, or anyone who approves letters.</summary>
    public static bool CanPreview(Letter letter, PathwayActor viewer) =>
        letter.Status is Draft or AwaitingApproval
        && (letter.AuthorUserId == viewer.UserId || letter.ApproverUserId == viewer.UserId || viewer.Has(Permission.ApproveLetters));

    /// <summary>Whether a correction can be written from it: withdrawn after approval, and none written yet.</summary>
    public static bool CanBeCorrected(Letter letter, bool hasCorrection) =>
        letter.Status == Withdrawn && letter.ApprovedAtUtc is not null && !hasCorrection;

    /// <summary>Whether one recipient can still be sent the letter: it's approved and they haven't had it.</summary>
    public static bool AwaitsSending(Letter letter, LetterRecipient recipient) =>
        letter.Status == Approved && recipient.DispatchStatus != LetterDispatchStatus.Sent;

    /// <summary>
    /// Why a recipient can't be sent the letter by secure email from MedApp (it's approved and unsent), or null if it
    /// can: the relay must be available, the recipient not the patient, and their address in an allowed domain.
    /// </summary>
    public static string? WhyNoSecureEmail(
        LetterRecipient recipient, bool secureEmailAvailable, IReadOnlyList<string> allowedDomains) =>
        !secureEmailAvailable ? SecureEmailOffMessage
        : recipient.Kind == LetterRecipientKind.Patient ? PatientEmailMessage
        : string.IsNullOrWhiteSpace(recipient.Email) ? NoSecureEmailAddressMessage
        : !Practices.Common.PracticeRules.IsInAllowedDomain(recipient.Email, allowedDomains)
            ? Practices.Common.PracticeRules.SecureEmailDomainMessage(allowedDomains)
        : null;

    /// <summary>"Discharge summary", for messages: the kind in words, starting lower case.</summary>
    public static string KindInMessage(LetterKind kind)
    {
        var words = LetterKinds.Describe(kind);
        return char.ToLower(words[0], CultureInfo.InvariantCulture) + words[1..];
    }
}
