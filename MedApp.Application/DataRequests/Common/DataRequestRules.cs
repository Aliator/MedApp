using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Domain.DataRequests;

namespace MedApp.Application.DataRequests.Common;

/// <summary>
/// The rules of a data request (<c>docs/centre-phases-3-5-plan.md</c>, decisions 160 to 163): the due date, who may do
/// what, and when the pack can be made. Every message is here.
/// </summary>
public static class DataRequestRules
{
    /// <summary>How far back a request's receipt may be logged.</summary>
    public const int MaxDaysBack = 366;

    /// <summary>How many days before the due date holders of ManagePrivacy are told.</summary>
    public const int DueSoonDays = 7;

    public const string ChangedMeanwhileMessage =
        "Someone else changed this data request meanwhile. Reload it to see their change, then try again.";

    public const string NotOpenMessage = "This data request is closed (sent, refused or withdrawn), so it can't change.";

    public const string AnonymisedMessage = "This patient's record has been anonymised, so its data requests can't change.";

    public const string CantHandleMessage = "Only holders of ManagePrivacy (admins) can see or handle data requests.";

    /// <summary>
    /// One calendar month from receipt, plus any extension: the same day of the month, or its last day when there's no
    /// matching day (31 January is due on 28 or 29 February), as the ICO counts it.
    /// </summary>
    public static DateOnly DueOn(DateOnly receivedOn, int extensionMonths) => receivedOn.AddMonths(1 + extensionMonths);

    /// <summary>The first month's due date, without any extension.</summary>
    public static DateOnly FirstDueOn(DataRequest request) => DueOn(request.ReceivedOn, 0);

    public static bool IsOverdue(DataRequest request, DateOnly today) => request.IsOpen && today > request.DueOn;

    /// <summary>
    /// Whether the caller may read or handle data requests: only holders of ManagePrivacy (admins), who log, review
    /// and answer them (the owner's decision: requests reach the company by email or its website and go straight to
    /// admins; only admins deal with them).
    /// </summary>
    public static bool CanHandle(PathwayActor actor) => actor.Has(Permission.ManagePrivacy);

    /// <summary>Every data-request handler's first check (403 otherwise), besides the endpoint's own.</summary>
    public static void EnsureCanHandle(PathwayActor actor)
    {
        if (!CanHandle(actor))
            throw new ForbiddenException(CantHandleMessage);
    }

    public static void EnsureVersion(DataRequest request, int expectedVersion)
    {
        if (request.Version != expectedVersion)
            throw new ConflictException(ChangedMeanwhileMessage);
    }

    /// <summary>
    /// Why the request can't change, or null: it's closed, or the patient's record is anonymised. A restricted record's
    /// requests can still change: restriction limits what's done with the data, not the patient's right of access.
    /// </summary>
    public static string? WhyCantChange(DataRequest request) =>
        PatientRules.IsAnonymised(request.Patient) ? AnonymisedMessage
        : !request.IsOpen ? NotOpenMessage
        : null;

    public static void EnsureCanChange(DataRequest request, int expectedVersion)
    {
        if (WhyCantChange(request) is { } why)
            throw new ConflictException(why);
        EnsureVersion(request, expectedVersion);
    }

    /// <summary>Why the pack can't be made yet, or null (decision 163: the identity check, then the review, come first).</summary>
    public static string? WhyNoPack(DataRequest request) =>
        WhyCantChange(request) is { } why ? why
        : !request.IsIdentityChecked ? "Record the requester's identity check before making the pack."
        : !request.IsReviewed ? "Review the record (and withhold anything that meets the serious harm test) before the pack is made."
        : null;

    /// <summary>Why a date can't be the receipt date, or null: not in the future, at most a year back.</summary>
    public static string? WhyReceivedOnInvalid(DateOnly receivedOn, DateOnly today) =>
        receivedOn > today ? "The date received can't be in the future."
        : receivedOn < today.AddDays(-MaxDaysBack) ? "The date received can't be more than a year ago."
        : null;

    /// <summary>Why a date can't be when something happened to the request (checked, sent), or null.</summary>
    public static string? WhyEventDateInvalid(DataRequest request, DateOnly date, DateOnly today, string what) =>
        date > today ? $"The date {what} can't be in the future."
        : date < request.ReceivedOn ? $"The date {what} can't be before the request was received."
        : null;

    /// <summary>
    /// Why the due date can't be extended now, or null: only within the first month (the requester must be told within
    /// it), and to at most two more months.
    /// </summary>
    public static string? WhyCantExtend(DataRequest request, DateOnly today) =>
        today > FirstDueOn(request)
            ? "The due date can only be extended within the first month, as the requester must be told within it."
            : null;
}
