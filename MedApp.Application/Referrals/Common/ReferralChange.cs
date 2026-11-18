using MedApp.Application.Pathways.Common;
using MedApp.Domain.Referrals;
using MedApp.Domain.Specialties;
using static MedApp.Domain.Referrals.ReferralStatus;

namespace MedApp.Application.Referrals.Common;

/// <summary>
/// Applies one request's change to a referral and collects its history entries (<see cref="ReferralEvent"/>), to save
/// in the same <c>SaveChangesAsync</c> as the change, so the history can't miss one. It moves the referral's
/// <see cref="Referral.Version"/> on once per change and stamps <see cref="Referral.LastUpdated"/>. Handlers check
/// that a change is allowed first (<see cref="ReferralWorkflow"/>); this only applies it.
/// <para>
/// Entries hold structured values only (statuses, the priority, the specialty's ID and name, dates, and the names of
/// the fields an edit changed), <b>never free text</b>: the reason, the referrer and the detail given live only on
/// the referral (DPIA risk 27).
/// </para>
/// </summary>
public sealed class ReferralChange(Referral referral, PathwayActor actor, DateTime nowUtc)
{
    /// <summary>The fields an edit names when it changes them, as the history and the timeline show them.</summary>
    public const string SpecialtyField = "specialty";
    public const string ReceivedOnField = "receivedOn";
    public const string ReferrerPracticeField = "referrerPractice";
    public const string ReferrerClinicianField = "referrerClinician";
    public const string ReferrerOdsCodeField = "referrerOdsCode";
    public const string RequestedPriorityField = "requestedPriority";
    public const string ReasonField = "reason";

    /// <summary>The practice chosen from the directory (by its ID; the typed practice is <see cref="ReferrerPracticeField"/>).</summary>
    public const string DirectoryPracticeField = "directoryPractice";

    private readonly List<ReferralEvent> _events = [];

    public Referral Referral => referral;

    public IReadOnlyList<ReferralEvent> Events => _events;

    public bool HasChanges => _events.Count > 0;

    /// <summary>Records a new referral (its <see cref="Referral.Version"/> is 1), with the specialty and priority asked for.</summary>
    public void Register(Specialty specialty)
    {
        referral.Version = 0;
        Record(ReferralEventAction.Registered, new
        {
            status = referral.Status.ToString(),
            specialtyId = specialty.Id,
            specialtyName = specialty.Name,
            receivedOn = referral.ReceivedOn.ToString("yyyy-MM-dd"),
            requestedPriority = referral.RequestedPriority.ToString()
        });
    }

    /// <summary>
    /// Replaces the referral's details, recording which fields changed and, for structured ones (the specialty, the
    /// date received and the priority), their values before and after; free text only by name. Nothing is recorded if
    /// nothing changed. <paramref name="specialty"/> is the one <paramref name="details"/> names, and
    /// <paramref name="previousSpecialty"/> the referral's before.
    /// </summary>
    public bool EditDetails(ReferralDetails details, Specialty specialty, Specialty? previousSpecialty)
    {
        var fields = new List<string>();
        var changes = new Dictionary<string, object>();

        if (referral.SpecialtyId != details.SpecialtyId)
        {
            fields.Add(SpecialtyField);
            changes[SpecialtyField] = new
            {
                fromId = referral.SpecialtyId,
                from = previousSpecialty?.Name,
                toId = specialty.Id,
                to = specialty.Name
            };
            // The ID only: the handler, which read the specialty without tracking, shows it after saving.
            referral.SpecialtyId = details.SpecialtyId;
        }

        if (referral.ReceivedOn != details.ReceivedOn)
        {
            fields.Add(ReceivedOnField);
            changes[ReceivedOnField] = new
            {
                from = referral.ReceivedOn.ToString("yyyy-MM-dd"),
                to = details.ReceivedOn.ToString("yyyy-MM-dd")
            };
            referral.ReceivedOn = details.ReceivedOn;
        }

        if (referral.RequestedPriority != details.RequestedPriority)
        {
            fields.Add(RequestedPriorityField);
            changes[RequestedPriorityField] = new
            {
                from = referral.RequestedPriority.ToString(),
                to = details.RequestedPriority.ToString()
            };
            referral.RequestedPriority = details.RequestedPriority;
        }

        // Free text: which field changed, never what it said (the letter is the record of that).
        if (referral.ReferrerPractice != details.ReferrerPractice)
        {
            fields.Add(ReferrerPracticeField);
            referral.ReferrerPractice = details.ReferrerPractice;
        }

        if (referral.ReferrerClinician != details.ReferrerClinician)
        {
            fields.Add(ReferrerClinicianField);
            referral.ReferrerClinician = details.ReferrerClinician;
        }

        if (referral.ReferrerOdsCode != details.ReferrerOdsCode)
        {
            fields.Add(ReferrerOdsCodeField);
            referral.ReferrerOdsCode = details.ReferrerOdsCode;
        }

        if (referral.PracticeId != details.PracticeId)
        {
            fields.Add(DirectoryPracticeField);
            changes[DirectoryPracticeField] = new { fromId = referral.PracticeId, toId = details.PracticeId };
            referral.PracticeId = details.PracticeId;
        }

        if (referral.Reason != details.Reason)
        {
            fields.Add(ReasonField);
            referral.Reason = details.Reason;
        }

        if (fields.Count == 0)
            return false;

        Record(ReferralEventAction.DetailsEdited, new { fields, changes });
        return true;
    }

    /// <summary>Withdraws it (the referrer or the patient withdrew it), with a detail kept on the referral.</summary>
    public void Withdraw(string detail) => Close(Withdrawn, ReferralEventAction.Withdrawn, detail);

    /// <summary>Marks it entered in error (the wrong patient, or registered twice), with a detail kept on the referral.</summary>
    public void MarkEnteredInError(string detail) => Close(EnteredInError, ReferralEventAction.EnteredInError, detail);

    private void Close(ReferralStatus status, ReferralEventAction action, string detail)
    {
        var previous = referral.Status;
        referral.Status = status;
        referral.DecidedAtUtc = nowUtc;
        referral.DecidedByUserId = actor.UserId;
        referral.DecidedByUsername = actor.Username;
        referral.DecisionDetail = detail;
        Record(action, new { from = previous.ToString(), to = status.ToString() });
    }

    /// <summary>
    /// Records a history entry. Entries one change writes are each a tick (100 ns) after the one before, so they sort in
    /// the order they happened, as the pathway audit log's do.
    /// </summary>
    private void Record(ReferralEventAction action, object details)
    {
        if (_events.Count == 0)
            referral.Version++;

        _events.Add(new ReferralEvent
        {
            Id = Guid.NewGuid(),
            ReferralId = referral.Id,
            PatientId = referral.PatientId,
            PathwayId = referral.PathwayId,
            Action = action,
            AtUtc = nowUtc.AddTicks(_events.Count),
            UserId = actor.UserId,
            Username = actor.Username,
            Details = PathwayAudit.Details(details)
        });
        referral.LastUpdated = nowUtc;
    }
}
