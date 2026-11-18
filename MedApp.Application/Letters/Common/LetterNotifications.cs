using MedApp.Application.Pathways.Common;
using MedApp.Domain.Letters;
using MedApp.Domain.Notifications;

namespace MedApp.Application.Letters.Common;

/// <summary>
/// What letters tell people (decision 102; kinds 29 to 31): fixed titles, and messages from usernames and the letter's
/// kind only, never the patient, the text or the return comment. Each carries the letter as its
/// <see cref="Notification.SubjectId"/>, which the web app opens.
/// </summary>
public static class LetterNotifications
{
    public const string AwaitingApprovalTitle = "Letter to approve";
    public const string ApprovedTitle = "Letter approved";
    public const string ReturnedTitle = "Letter returned";

    /// <summary>To the approver it was submitted to (never the author themselves: they can't submit to themselves).</summary>
    public static Notification AwaitingApproval(Letter letter, PathwayActor author, DateTime nowUtc) =>
        Make(letter.ApproverUserId!.Value, NotificationKind.LetterAwaitingApproval, AwaitingApprovalTitle,
            $"{author.Username} asked you to approve {Article(letter.Kind)} {LetterWorkflow.KindInMessage(letter.Kind)}.",
            letter, nowUtc);

    /// <summary>To the author, when someone else approved it; null when they approved it themselves.</summary>
    public static Notification? Approved(Letter letter, PathwayActor approver, DateTime nowUtc) =>
        letter.AuthorUserId == approver.UserId
            ? null
            : Make(letter.AuthorUserId, NotificationKind.LetterApproved, ApprovedTitle,
                $"{approver.Username} approved your {LetterWorkflow.KindInMessage(letter.Kind)}.",
                letter, nowUtc);

    /// <summary>To the author, with no word of the comment (it's on the letter's page).</summary>
    public static Notification Returned(Letter letter, PathwayActor approver, DateTime nowUtc) =>
        Make(letter.AuthorUserId, NotificationKind.LetterReturned, ReturnedTitle,
            $"{approver.Username} returned your {LetterWorkflow.KindInMessage(letter.Kind)} with a comment.",
            letter, nowUtc);

    private static Notification Make(
        Guid userId, NotificationKind kind, string title, string message, Letter letter, DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Kind = kind,
        Title = title,
        Message = message,
        PathwayId = letter.PathwayId,
        ReferralId = letter.ReferralId,
        SubjectId = letter.Id,
        CreatedAtUtc = nowUtc,
    };

    /// <summary>"a" or "an" before the kind in words ("an appointment letter").</summary>
    private static string Article(LetterKind kind) =>
        "aeiou".Contains(LetterWorkflow.KindInMessage(kind)[0]) ? "an" : "a";
}
