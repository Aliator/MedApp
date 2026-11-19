using MedApp.Domain.Activity;

namespace MedApp.Application.Activity.Common;

/// <summary>
/// Writes the activity log: who read or changed patient data, signed in or out, or changed an account. Entries are
/// made by the current request's actor (<see cref="IActivityContext"/>) at the current time.
/// <para>
/// A change to a patient saves its entry in the same save as the change (make it with <see cref="Entry"/> and hand it
/// to the repository), so no change can miss the log. Views, sign-ins and account changes are recorded on their own.
/// </para>
/// </summary>
public interface IActivityLog
{
    /// <summary>Who this request is by, as entries record them.</summary>
    ActivityActor Actor { get; }

    /// <summary>
    /// A new entry, by <paramref name="actor"/> (the request's actor if null), now. Not saved: pass it to the
    /// repository saving the change, or to <see cref="RecordAsync"/>. <paramref name="details"/> is serialised to
    /// camelCase JSON; it must never hold a patient's details or clinical text.
    /// </summary>
    ActivityEntry Entry(
        ActivityAction action,
        Guid? patientId = null,
        object? details = null,
        string? targetUsername = null,
        Guid? pathwayId = null,
        ActivityActor? actor = null);

    /// <summary>Saves an entry on its own.</summary>
    Task RecordAsync(ActivityEntry entry, CancellationToken ct);

    /// <summary>
    /// Records that the actor saw these patients (<see cref="ActivityAction.PatientViewed"/>,
    /// <see cref="ActivityAction.PatientListed"/>, or <see cref="ActivityAction.PathwayViewed"/> with
    /// <paramref name="pathwayId"/>), one entry each, except where the same view by the same person is already in
    /// the log from the last <see cref="ActivityOptions.ViewWindowMinutes"/> minutes.
    /// </summary>
    /// <remarks>
    /// <paramref name="details"/>, when given, go on each entry (a pathway view's <c>access</c>, decision 207 of
    /// <c>docs/multi-clinic-plan.md</c>); they never decide whether a view is new.
    /// </remarks>
    Task RecordViewsAsync(
        ActivityAction action, IEnumerable<Guid> patientIds, CancellationToken ct, Guid? pathwayId = null, object? details = null);

    /// <summary>
    /// Records that the actor saw one thing about a patient that <paramref name="details"/> names (a referral:
    /// <see cref="ActivityAction.ReferralViewed"/> with its ID), unless the same view, with the same details, by the same
    /// person is already in the log from the last <see cref="ActivityOptions.ViewWindowMinutes"/> minutes. The details
    /// must never hold a patient's details or clinical text.
    /// </summary>
    Task RecordViewAsync(ActivityAction action, Guid patientId, object details, CancellationToken ct);

    /// <summary>
    /// Records an entry about no patient (a refusal across a team wall: <see cref="ActivityAction.TeamBoundaryRefused"/>
    /// with what was asked for), unless the same entry, with the same details, by the same person is already in the log
    /// from the last <see cref="ActivityOptions.ViewWindowMinutes"/> minutes. The details must never hold a patient's
    /// details or clinical text.
    /// </summary>
    Task RecordOnceAsync(ActivityAction action, object details, CancellationToken ct);
}
