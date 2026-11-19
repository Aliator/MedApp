using MedApp.Application.Pathways.Common;
using MedApp.Domain.Lab;
using MedApp.Domain.Notifications;
using MedApp.Domain.Specimens;

namespace MedApp.Application.Lab.Common;

/// <summary>
/// What lab cases tell people (MC15): a fixed title, and a message that names only the test (admin-written) and the
/// stage reopened, never the patient or why.
/// </summary>
public static class LabCaseNotifications
{
    public const string RepeatRequestedTitle = "Repeat sample requested";

    /// <summary>
    /// "The lab asked for a repeat sample (Histology). The Take biopsy stage is open again for the repeat.", to whoever
    /// asked for the sample and took it, never the actor.
    /// </summary>
    public static IReadOnlyList<Notification> RepeatRequested(
        LabCase labCase, Specimen sample, string? reopenedStage, PathwayActor actor, DateTime nowUtc)
    {
        var message = $"The lab asked for a repeat sample ({labCase.LabTest?.Name ?? "a lab test"})."
                      + (reopenedStage is null ? string.Empty : $" The {reopenedStage} stage is open again for the repeat.");
        return new[] { sample.RequestingUserId, sample.TakenByUserId }
            .Distinct()
            .Where(userId => userId != actor.UserId)
            .Select(userId => new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Kind = NotificationKind.RepeatSampleRequested,
                Title = RepeatRequestedTitle,
                Message = message,
                PathwayId = labCase.PathwayId,
                SubjectId = sample.Id,
                CreatedAtUtc = nowUtc,
            })
            .ToList();
    }
}
