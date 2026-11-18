using MedApp.Application.Pathways.Common;
using MedApp.Domain.Notifications;
using MedApp.Domain.Referrals;

namespace MedApp.Application.Referrals.Common;

/// <summary>
/// What whoever registered a referral is told when it's triaged (<see cref="NotificationKind.ReferralTriaged"/>), so
/// the booking team can tell the referrer. A fixed title, and a message from the triager's username, the outcome and
/// the specialty (an admin-written name) only: never the patient, the reason, the detail or where it was sent.
/// </summary>
public static class ReferralNotifications
{
    public const string TriagedTitle = "Referral triaged";

    /// <summary>
    /// The notification for the referral's registrant about the triage <paramref name="actor"/> just made, or null when
    /// they triaged it themselves (they know already). <paramref name="previousSpecialtyName"/> is the specialty it was
    /// in before a redirection to another one here.
    /// </summary>
    public static Notification? Triaged(
        Referral referral, PathwayActor actor, DateTime nowUtc, string? previousSpecialtyName = null)
    {
        if (referral.RegisteredByUserId == actor.UserId)
            return null;

        return new Notification
        {
            Id = Guid.NewGuid(),
            UserId = referral.RegisteredByUserId,
            Kind = NotificationKind.ReferralTriaged,
            Title = TriagedTitle,
            Message = Message(referral, actor.Username, previousSpecialtyName),
            ReferralId = referral.Id,
            CreatedAtUtc = nowUtc
        };
    }

    /// <summary>"k.ashford accepted a Dermatology referral.", or for a move here, "... redirected a Dermatology referral to Urology."</summary>
    public static string Message(Referral referral, string username, string? previousSpecialtyName = null)
    {
        var specialty = referral.Specialty?.Name ?? "specialty";
        return referral.Status switch
        {
            ReferralStatus.Accepted => $"{username} accepted {Article(specialty)} {specialty} referral.",
            ReferralStatus.Rejected => $"{username} rejected {Article(specialty)} {specialty} referral.",
            ReferralStatus.RedirectedElsewhere => $"{username} redirected {Article(specialty)} {specialty} referral elsewhere.",
            _ when previousSpecialtyName is not null =>
                $"{username} redirected {Article(previousSpecialtyName)} {previousSpecialtyName} referral to {specialty}.",
            _ => $"{username} triaged {Article(specialty)} {specialty} referral."
        };
    }

    /// <summary>"a" or "an", by the name's first letter: "an Urology referral" would read wrongly, so only vowels A, E, I, O.</summary>
    private static string Article(string name) =>
        name.Length > 0 && "AEIOaeio".Contains(name[0]) ? "an" : "a";
}
