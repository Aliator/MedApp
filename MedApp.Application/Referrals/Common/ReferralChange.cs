using MedApp.Application.Pathways.Common;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Referrals;
using MedApp.Domain.Teams;
using static MedApp.Domain.Referrals.ReferralStatus;

namespace MedApp.Application.Referrals.Common;

/// <summary>
/// Applies one request's change to a referral and collects its history entries (<see cref="ReferralEvent"/>), to save
/// in the same <c>SaveChangesAsync</c> as the change, so the history can't miss one. It moves the referral's
/// <see cref="Referral.Version"/> on once per change and stamps <see cref="Referral.LastUpdated"/>. Handlers check
/// that a change is allowed first (<see cref="ReferralWorkflow"/>); this only applies it.
/// <para>
/// Entries hold structured values only (statuses, the priority, the team's ID and name, dates, and the names of
/// the fields an edit changed), <b>never free text</b>: the reason, the referrer and the detail given live only on
/// the referral (DPIA risk 27).
/// </para>
/// </summary>
public sealed class ReferralChange(Referral referral, PathwayActor actor, DateTime nowUtc)
{
    /// <summary>The fields an edit names when it changes them, as the history and the timeline show them.</summary>
    public const string TeamField = "specialty";
    public const string ReceivedOnField = "receivedOn";
    public const string ReferrerPracticeField = "referrerPractice";
    public const string ReferrerClinicianField = "referrerClinician";
    public const string ReferrerOdsCodeField = "referrerOdsCode";
    public const string RequestedPriorityField = "requestedPriority";
    public const string ReasonField = "reason";

    /// <summary>Why a clock's target was re-read (<see cref="ReferralEventAction.TargetChanged"/>'s <c>cause</c>).</summary>
    public const string TargetCausePriority = "priority";
    public const string TargetCauseTeam = "specialty";

    /// <summary>The practice chosen from the directory (by its ID; the typed practice is <see cref="ReferrerPracticeField"/>).</summary>
    public const string DirectoryPracticeField = "directoryPractice";

    private readonly List<ReferralEvent> _events = [];

    public Referral Referral => referral;

    public IReadOnlyList<ReferralEvent> Events => _events;

    public bool HasChanges => _events.Count > 0;

    /// <summary>The UK date of the change: what the clock counts in.</summary>
    public DateOnly Today { get; } = DueDates.ToUkDate(nowUtc);

    /// <summary>
    /// Records a new referral (its <see cref="Referral.Version"/> is 1), with the team and priority asked for, and
    /// starts its waiting-time clock on the day it was received, with the team's target for that priority.
    /// </summary>
    public void Register(Team team)
    {
        referral.Version = 0;
        WaitingClock.Start(referral, WaitingClock.TargetDaysFor(team, referral.RequestedPriority));
        Record(ReferralEventAction.Registered, new
        {
            status = referral.Status.ToString(),
            specialtyId = team.Id,
            specialtyName = team.Name,
            receivedOn = referral.ReceivedOn.ToString("yyyy-MM-dd"),
            requestedPriority = referral.RequestedPriority.ToString(),
            targetDays = referral.TargetDays,
            targetOn = Date(referral.TargetOn)
        });
    }

    /// <summary>
    /// Replaces the referral's details, recording which fields changed and, for structured ones (the team, the
    /// date received and the priority), their values before and after; free text only by name. Nothing is recorded if
    /// nothing changed. <paramref name="team"/> is the one <paramref name="details"/> names, and
    /// <paramref name="previousTeam"/> the referral's before.
    /// </summary>
    public bool EditDetails(ReferralDetails details, Team team, Team? previousTeam)
    {
        var fields = new List<string>();
        var changes = new Dictionary<string, object>();

        if (referral.TeamId != details.TeamId)
        {
            fields.Add(TeamField);
            changes[TeamField] = new
            {
                fromId = referral.TeamId,
                from = previousTeam?.Name,
                toId = team.Id,
                to = team.Name
            };
            // The ID only: the handler, which read the team without tracking, shows it after saving.
            referral.TeamId = details.TeamId;
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

        // A correction of what registration copied the target from (the team or the priority asked for) re-reads
        // it; a corrected date received moves the dates it counts from.
        if (fields.Contains(TeamField) || fields.Contains(RequestedPriorityField))
            ChangeTarget(
                WaitingClock.TargetDaysFor(team, referral.RequestedPriority),
                fields.Contains(TeamField) ? TargetCauseTeam : TargetCausePriority);
        if (fields.Contains(ReceivedOnField))
            WaitingClock.Recalculate(referral);
        return true;
    }

    /// <summary>
    /// Accepts it into <paramref name="pathway"/>, a new pathway made from <paramref name="templateName"/> with the
    /// triager's <paramref name="priority"/>, saved with it. The history records the team, the template, the
    /// priority chosen and the one asked for, and the pathway; never free text. Its clock keeps running; re-read its
    /// target for the priority chosen first (<see cref="ReTarget"/>), as the pathway's target date is the clock's
    /// <see cref="Referral.TargetOn"/>.
    /// </summary>
    public void Accept(Guid pathwayId, string templateName, PatientPathwayPriority priority)
    {
        var previous = referral.Status;
        Decide(Accepted);
        referral.PathwayId = pathwayId;
        Record(ReferralEventAction.Accepted, new
        {
            from = previous.ToString(),
            to = Accepted.ToString(),
            specialtyId = referral.TeamId,
            specialtyName = referral.Team?.Name,
            templateName,
            priority = priority.ToString(),
            requestedPriority = referral.RequestedPriority.ToString(),
            pathwayId
        });
    }

    /// <summary>Rejects it, for a reason (a fixed choice) and an optional detail kept on the referral.</summary>
    public void Reject(ReferralRejectionReason reason, string? detail)
    {
        var previous = referral.Status;
        Decide(Rejected);
        referral.RejectionReason = reason;
        referral.DecisionDetail = detail;
        Record(ReferralEventAction.Rejected, new
        {
            from = previous.ToString(),
            to = Rejected.ToString(),
            specialtyId = referral.TeamId,
            specialtyName = referral.Team?.Name,
            rejectionReason = reason.ToString()
        });
        StopClock(ClockStopReason.Rejected);
    }

    /// <summary>
    /// Redirects it to another provider: <paramref name="elsewhere"/> (where to) and the detail are kept on the
    /// referral, never in its history. It closes it.
    /// </summary>
    public void RedirectElsewhere(string elsewhere, string detail)
    {
        var previous = referral.Status;
        Decide(RedirectedElsewhere);
        referral.RedirectedTo = elsewhere;
        referral.DecisionDetail = detail;
        Record(ReferralEventAction.RedirectedElsewhere, new
        {
            from = previous.ToString(),
            to = RedirectedElsewhere.ToString(),
            specialtyId = referral.TeamId,
            specialtyName = referral.Team?.Name
        });
        StopClock(ClockStopReason.RedirectedElsewhere);
    }

    /// <summary>
    /// Redirects it to another team here: it stays awaiting triage, in <paramref name="team"/>'s inbox, from
    /// the same date received. The detail is kept on the referral (<see cref="Referral.RedirectDetail"/>); the history
    /// records the teams before and after. Its clock keeps running from the same day, with the target re-read
    /// from the new team's table.
    /// </summary>
    public void RedirectToTeam(Team team, string detail)
    {
        var previousId = referral.TeamId;
        var previousName = referral.Team?.Name;
        // The ID only, as an edit does: the handler, which read the team without tracking, shows it after saving.
        referral.TeamId = team.Id;
        referral.RedirectDetail = detail;
        Record(ReferralEventAction.TeamChanged, new
        {
            specialtyId = team.Id,
            specialtyName = team.Name,
            previousSpecialtyId = previousId,
            previousSpecialtyName = previousName
        });
        ChangeTarget(WaitingClock.TargetDaysFor(team, referral.RequestedPriority), TargetCauseTeam);
    }

    /// <summary>
    /// A triager asked the referrer for more information (decision 106): it stays open, waiting in its own view. Not a
    /// decision, so the decision columns stay empty.
    /// </summary>
    public void AskForInformation()
    {
        var previous = referral.Status;
        referral.Status = InformationRequested;
        Record(ReferralEventAction.InformationRequested, new { from = previous.ToString(), to = InformationRequested.ToString() });
    }

    /// <summary>The information asked for came: it's back awaiting triage.</summary>
    public void InformationReceived()
    {
        var previous = referral.Status;
        referral.Status = AwaitingTriage;
        Record(ReferralEventAction.InformationReceived, new { from = previous.ToString(), to = AwaitingTriage.ToString() });
    }

    /// <summary>Withdraws it (the referrer or the patient withdrew it), with a detail kept on the referral.</summary>
    public void Withdraw(string detail) => Close(Withdrawn, ReferralEventAction.Withdrawn, detail);

    /// <summary>Marks it entered in error (the wrong patient, or registered twice), with a detail kept on the referral.</summary>
    public void MarkEnteredInError(string detail) => Close(EnteredInError, ReferralEventAction.EnteredInError, detail);

    private void Close(ReferralStatus status, ReferralEventAction action, string detail)
    {
        var previous = referral.Status;
        Decide(status);
        referral.DecisionDetail = detail;
        Record(action, new { from = previous.ToString(), to = status.ToString() });
        if (WaitingClock.StopReasonFor(status) is { } stopReason)
            StopClock(stopReason);
    }

    /// <summary>
    /// Re-reads the clock's target for <paramref name="priority"/> from <paramref name="team"/>'s table (a
    /// <see cref="ReferralEventAction.TargetChanged"/> entry, if the days differ): when the triager chooses the priority,
    /// and when a running or paused clock's pathway changes priority. Nothing once the clock has stopped.
    /// </summary>
    public bool ReTarget(Team team, PatientPathwayPriority priority) =>
        ChangeTarget(WaitingClock.TargetDaysFor(team, priority), TargetCausePriority);

    /// <summary>Sets the clock's target in days, recording the old and new (nothing if unchanged or stopped).</summary>
    private bool ChangeTarget(int targetDays, string cause)
    {
        if (WaitingClock.IsStopped(referral) || targetDays == referral.TargetDays)
            return false;

        var previous = referral.TargetDays;
        referral.TargetDays = targetDays;
        WaitingClock.Recalculate(referral);
        Record(ReferralEventAction.TargetChanged, new
        {
            fromDays = previous,
            toDays = targetDays,
            targetOn = Date(referral.TargetOn),
            cause
        });
        return true;
    }

    /// <summary>
    /// Pauses the clock today, for a reason the patient chose or caused, until <paramref name="reviewOn"/>. The handler
    /// checks first that it may (<see cref="WaitingClock.WhyCantPause"/>). The history records the reason by name and
    /// the review date, never the detail, which is kept on the pause.
    /// </summary>
    public ReferralClockPause PauseClock(ClockPauseReason reason, string? detail, DateOnly reviewOn)
    {
        var pause = new ReferralClockPause
        {
            Id = Guid.NewGuid(),
            ReferralId = referral.Id,
            PatientId = referral.PatientId,
            PausedOn = Today,
            PausedAtUtc = nowUtc,
            PausedByUserId = actor.UserId,
            PausedByUsername = actor.Username,
            Reason = reason,
            Detail = detail,
            ReviewOn = reviewOn
        };
        referral.Pauses.Add(pause);
        referral.PausedSinceOn = Today;
        referral.PauseReviewOn = reviewOn;
        Record(ReferralEventAction.ClockPaused, new
        {
            pauseId = pause.Id,
            pauseReason = reason.ToString(),
            pausedOn = Date(Today),
            reviewOn = Date(reviewOn)
        });
        return pause;
    }

    /// <summary>
    /// Ends the open pause today: its days are added to the paused days, so the wait counts from later and the target
    /// moves on as far. <paramref name="automatic"/> when the clock stopping ended it.
    /// </summary>
    public void ResumeClock(ReferralClockPause pause, bool automatic = false)
    {
        pause.ResumedOn = Today;
        pause.ResumedAtUtc = nowUtc;
        pause.ResumedByUserId = actor.UserId;
        pause.ResumedByUsername = actor.Username;

        referral.PausedDays += pause.Days;
        referral.PausedSinceOn = null;
        referral.PauseReviewOn = null;
        WaitingClock.Recalculate(referral);
        Record(ReferralEventAction.ClockResumed, new
        {
            pauseId = pause.Id,
            pauseReason = pause.Reason.ToString(),
            pausedDays = pause.Days,
            targetOn = Date(referral.TargetOn),
            automatic = automatic ? true : (bool?)null
        });
    }

    /// <summary>
    /// Stops the clock today, for good (nothing if it has stopped already). An open pause ends first, so its days don't
    /// count. The history records why, the days waited, the target and whether it was breached.
    /// </summary>
    public void StopClock(ClockStopReason reason)
    {
        if (WaitingClock.IsStopped(referral))
            return;

        if (WaitingClock.IsPaused(referral) && WaitingClock.OpenPause(referral) is { } open)
            ResumeClock(open, automatic: true);

        referral.StoppedOn = Today;
        referral.StopReason = reason;
        Record(ReferralEventAction.ClockStopped, new
        {
            stopReason = reason.ToString(),
            stoppedOn = Date(Today),
            daysWaited = WaitingClock.DaysWaited(referral, Today),
            targetDays = referral.TargetDays,
            breached = WaitingClock.IsBreached(referral, Today)
        });
    }

    private static string Date(DateOnly date) => date.ToString("yyyy-MM-dd");

    /// <summary>Moves it out of Awaiting triage, by the actor, now.</summary>
    private void Decide(ReferralStatus status)
    {
        referral.Status = status;
        referral.DecidedAtUtc = nowUtc;
        referral.DecidedByUserId = actor.UserId;
        referral.DecidedByUsername = actor.Username;
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
