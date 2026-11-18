using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Domain.Calendar;
using MedApp.Domain.Mdt;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Mdt.Common;

/// <summary>
/// The rules for MDT items (<c>docs/centre-phases-3-5-plan.md</c>, decisions 141 to 144). Listing and deferring are new
/// work, refused while the record is restricted or anonymised (decision 75); withdrawing and recording the team's
/// outcome finish work already under way, so they're allowed while restricted, never once anonymised. MedApp records the
/// outcome a person wrote; it never suggests one or changes the pathway because of it.
/// </summary>
public static class MdtRules
{
    public const string ChangedMeanwhileMessage =
        "Someone else changed this MDT item since you opened it. Reload it, then try again.";
    public const string NotMdtMessage = "That meeting isn't an MDT.";
    public const string MeetingEndedMessage = "That MDT has already ended: choose a later one.";
    public const string AlreadyListedMessage = "This pathway is already on that MDT's list.";
    public const string PathwayClosedMessage = "A discharged, cancelled or archived pathway can't be listed for an MDT.";
    public const string ClosedMessage = "This MDT item has been discussed or withdrawn, so it can't change.";
    public const string NotStartedMessage = "Its outcome can be recorded once its meeting has started.";
    public const string NoMeetingMessage = "Its meeting was cancelled: list it for another MDT first.";
    public const string NotLaterMessage = "Choose a later MDT than the one it's on.";
    public const string NotPresentMessage = "Choose who was present from the meeting's attendees.";
    public const string HasOutcomesMessage = "Outcomes are recorded on this MDT, so it can't be cancelled.";
    public const string HasItemsMessage =
        "Patients are listed for this MDT: move or withdraw them before changing its specialty or making it an ordinary meeting.";

    /// <summary>A 409 unless the version is the item's.</summary>
    public static void EnsureVersion(MdtItem item, int expectedVersion)
    {
        if (item.Version != expectedVersion)
            throw new ConflictException(ChangedMeanwhileMessage);
    }

    /// <summary>A 409 unless the item is still to be discussed.</summary>
    public static void EnsureOpen(MdtItem item)
    {
        if (!item.IsOpen)
            throw new ConflictException(ClosedMessage);
    }

    /// <summary>
    /// A 409 unless the meeting is an MDT that hasn't ended. <paramref name="after"/>, when given, is the start of the
    /// meeting an item is on now: the new one must start after it.
    /// </summary>
    public static void EnsureCanListFor(Meeting meeting, DateTime nowUtc, DateTime? after = null)
    {
        if (!meeting.IsMdt || meeting.SpecialtyId is null)
            throw new ConflictException(NotMdtMessage);
        if (meeting.EndUtc <= nowUtc)
            throw new ConflictException(MeetingEndedMessage);
        if (after is { } start && meeting.StartUtc <= start)
            throw new ConflictException(NotLaterMessage);
    }

    /// <summary>A 409 unless the pathway is open or on hold (not discharged, cancelled or archived).</summary>
    public static void EnsurePathwayCanBeListed(PatientPathway pathway)
    {
        if (PathwayWorkflow.IsClosed(pathway.Status))
            throw new ConflictException(PathwayClosedMessage);
    }

    /// <summary>A 409 unless the outcome can be recorded now: the item is open, on a meeting that has started.</summary>
    public static void EnsureCanRecordOutcome(MdtItem item, DateTime nowUtc)
    {
        EnsureOpen(item);
        if (item.Meeting is null)
            throw new ConflictException(NoMeetingMessage);
        if (item.Meeting.StartUtc > nowUtc)
            throw new ConflictException(NotStartedMessage);
    }

    /// <summary>Whether the viewer can withdraw it: they work on pathways, it's open and the record isn't anonymised.</summary>
    public static bool CanWithdraw(MdtItem item, PathwayActor viewer) =>
        viewer.Has(Permission.WorkPathways) && item.IsOpen && !PatientRules.IsAnonymised(item.Patient);

    /// <summary>Whether the viewer can move it to another meeting: they work on pathways, it's open and the record can change.</summary>
    public static bool CanDefer(MdtItem item, PathwayActor viewer) =>
        viewer.Has(Permission.WorkPathways) && item.IsOpen && PatientRules.CanChange(item.Patient);

    /// <summary>Whether the viewer can record its outcome: they hold RecordMdtOutcomes, and it can be recorded now.</summary>
    public static bool CanRecordOutcome(MdtItem item, PathwayActor viewer, DateTime nowUtc) =>
        viewer.Has(Permission.RecordMdtOutcomes)
        && item.IsOpen
        && item.Meeting is { } meeting && meeting.StartUtc <= nowUtc
        && !PatientRules.IsAnonymised(item.Patient);
}
