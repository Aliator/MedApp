using MedApp.Application.Pathways.Common;
using MedApp.Domain.Notifications;
using MedApp.Domain.Results;

namespace MedApp.Application.Results.Common;

/// <summary>
/// Who is told about a result: its responsible clinician, once when it's ready, when it's given to them, and when
/// someone else signs it off; never whoever did it. Fixed titles, and messages built from usernames and the specimen
/// type's name (admin-written): never the patient, the title, the report or any detail (<see cref="Notification.HasFixedTitle"/>).
/// Each links to the result's page (<see cref="Notification.SubjectId"/>). There's no reminder per day waiting: the
/// results list's count is the signal (decision 94).
/// </summary>
public static class ResultNotifications
{
    public const string ReadyTitle = "Result ready";
    public const string ReassignedTitle = "Result reassigned to you";
    public const string SignedOffTitle = "Result signed off";

    /// <summary>"k.lab reported a Skin biopsy (punch) specimen.", or "s.nurse recorded a result from another service."</summary>
    public static Notification? Ready(Result result, PathwayActor actor, DateTime nowUtc) =>
        For(result, actor, nowUtc, NotificationKind.ResultReady, ReadyTitle,
            result.Specimen?.SpecimenType?.Name is { Length: > 0 } type
                ? $"{actor.Username} reported {Article(type)} sample."
                : $"{actor.Username} recorded a result from another service.");

    /// <summary>"k.ashford made you responsible for a result."</summary>
    public static Notification? Reassigned(Result result, PathwayActor actor, DateTime nowUtc) =>
        For(result, actor, nowUtc, NotificationKind.ResultReassigned, ReassignedTitle,
            $"{actor.Username} made you responsible for a result.");

    /// <summary>"k.ashford signed off a result you're responsible for."</summary>
    public static Notification? SignedOffByOther(Result result, PathwayActor actor, DateTime nowUtc) =>
        For(result, actor, nowUtc, NotificationKind.ResultSignedOffByOther, SignedOffTitle,
            $"{actor.Username} signed off a result you're responsible for.");

    private static Notification? For(
        Result result, PathwayActor actor, DateTime nowUtc, NotificationKind kind, string title, string message) =>
        result.ResponsibleUserId == actor.UserId
            ? null
            : new Notification
            {
                Id = Guid.NewGuid(),
                UserId = result.ResponsibleUserId,
                Kind = kind,
                Title = title,
                Message = message,
                PathwayId = result.PathwayId,
                SubjectId = result.Id,
                CreatedAtUtc = nowUtc
            };

    /// <summary>"a Skin biopsy (punch)", or "an Endometrial biopsy".</summary>
    private static string Article(string name) =>
        ("AEIOU".Contains(char.ToUpperInvariant(name[0])) ? "an " : "a ") + name;
}
