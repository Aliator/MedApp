using MedApp.Application.Pathways.Common;
using MedApp.Domain.Notifications;
using MedApp.Domain.Specimens;

namespace MedApp.Application.Specimens.Common;

/// <summary>
/// Who is told when a specimen won't come back: the requesting clinician and whoever took it, never whoever recorded
/// it (they know). Fixed titles, and messages built from usernames and the specimen type's name (admin-written): never
/// the patient, the site, the request notes or the detail typed (<see cref="Notification.HasFixedTitle"/>). Each links
/// to the specimen's page (<see cref="Notification.SubjectId"/>), so someone can take another.
/// </summary>
public static class SpecimenNotifications
{
    public const string RejectedTitle = "Specimen rejected";
    public const string LostTitle = "Specimen lost";

    /// <summary>"The lab rejected a Skin biopsy (punch) specimen."</summary>
    public static IReadOnlyList<Notification> Rejected(Specimen specimen, PathwayActor actor, DateTime nowUtc) =>
        For(specimen, actor, nowUtc, NotificationKind.SpecimenRejectedByLab, RejectedTitle,
            $"The lab rejected {Article(specimen)} specimen.");

    /// <summary>"k.ashford marked a Skin biopsy (punch) specimen lost."</summary>
    public static IReadOnlyList<Notification> Lost(Specimen specimen, PathwayActor actor, DateTime nowUtc) =>
        For(specimen, actor, nowUtc, NotificationKind.SpecimenLost, LostTitle,
            $"{actor.Username} marked {Article(specimen)} specimen lost.");

    private static IReadOnlyList<Notification> For(
        Specimen specimen, PathwayActor actor, DateTime nowUtc, NotificationKind kind, string title, string message) =>
        new[] { specimen.RequestingUserId, specimen.TakenByUserId }
            .Distinct()
            .Where(userId => userId != actor.UserId)
            .Select(userId => new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Kind = kind,
                Title = title,
                Message = message,
                PathwayId = specimen.PathwayId,
                SubjectId = specimen.Id,
                CreatedAtUtc = nowUtc
            })
            .ToList();

    /// <summary>"a Skin biopsy (punch)", or "an Endometrial biopsy".</summary>
    private static string Article(Specimen specimen)
    {
        var name = specimen.SpecimenType?.Name is { Length: > 0 } typeName ? typeName : "lab";
        return ("AEIOU".Contains(char.ToUpperInvariant(name[0])) ? "an " : "a ") + name;
    }
}
