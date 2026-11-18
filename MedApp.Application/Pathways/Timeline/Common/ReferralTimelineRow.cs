using MedApp.Domain.Referrals;

namespace MedApp.Application.Pathways.Timeline.Common;

/// <summary>
/// A referral history entry on the timeline, with the free text its referral holds now, for admins only
/// (<see cref="TimelineMapper"/>): the referral's reason, its referring practice, the detail given when it was
/// triaged or closed, where it was redirected elsewhere, and why it was redirected here from another specialty. The
/// entry itself holds none.
/// </summary>
public sealed record ReferralTimelineRow(
    ReferralEvent Event,
    string? Reason = null,
    string? ReferrerPractice = null,
    string? DecisionDetail = null,
    string? RedirectedTo = null,
    string? RedirectDetail = null);
