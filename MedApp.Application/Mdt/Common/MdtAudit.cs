using System.Text.Json;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Calendar;
using MedApp.Domain.Mdt;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Mdt.Common;

/// <summary>
/// Audit entries for MDT items, in the pathway audit log, so they're on the patient's timeline (the Mdt kind) and in the
/// admins' log (decision 70). The details hold the item's ID, the team's name, the meetings' IDs and starts, and
/// who was present: <b>never</b> the question or the outcome, which live only on the item. An item isn't a change to the
/// pathway, so these don't go through <see cref="PathwayChange"/> and don't move its LastUpdated.
/// </summary>
public static class MdtAudit
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static PatientPathwayAuditEntry Listed(
        MdtItem item, PatientPathway pathway, string team, Meeting meeting, PathwayActor actor, DateTime nowUtc) =>
        Entry(item, pathway, actor, PatientPathwayAuditAction.MdtListed, nowUtc, new
        {
            mdtItemId = item.Id,
            specialty = team,
            meetingId = meeting.Id,
            meetingStartUtc = meeting.StartUtc,
        });

    /// <summary>
    /// Moved to <paramref name="to"/> from the meeting it was on (<paramref name="fromMeetingId"/>, null when that was
    /// cancelled, with its start either way).
    /// </summary>
    public static PatientPathwayAuditEntry Deferred(
        MdtItem item, string team, Guid? fromMeetingId, DateTime? fromStartUtc, Meeting to, PathwayActor actor, DateTime nowUtc) =>
        Entry(item, item.Pathway, actor, PatientPathwayAuditAction.MdtDeferred, nowUtc, new
        {
            mdtItemId = item.Id,
            specialty = team,
            fromMeetingId,
            fromMeetingStartUtc = fromStartUtc,
            meetingId = to.Id,
            meetingStartUtc = to.StartUtc,
        });

    public static PatientPathwayAuditEntry Withdrawn(MdtItem item, string team, PathwayActor actor, DateTime nowUtc) =>
        Entry(item, item.Pathway, actor, PatientPathwayAuditAction.MdtWithdrawn, nowUtc, new
        {
            mdtItemId = item.Id,
            specialty = team,
            meetingId = item.MeetingId,
            meetingStartUtc = item.Meeting?.StartUtc,
        });

    public static PatientPathwayAuditEntry OutcomeRecorded(MdtItem item, string team, PathwayActor actor, DateTime nowUtc) =>
        Entry(item, item.Pathway, actor, PatientPathwayAuditAction.MdtOutcomeRecorded, nowUtc, new
        {
            mdtItemId = item.Id,
            specialty = team,
            meetingId = item.MeetingId,
            meetingStartUtc = item.Meeting?.StartUtc,
            present = item.Attendees.Select(a => a.Username).ToList(),
        });

    private static PatientPathwayAuditEntry Entry(
        MdtItem item, PatientPathway pathway, PathwayActor actor, PatientPathwayAuditAction action, DateTime nowUtc, object details) => new()
    {
        Id = Guid.NewGuid(),
        PatientPathwayId = pathway.Id,
        PathwayTitle = pathway.Title,
        PatientId = item.PatientId,
        TeamId = pathway.TeamId,
        Action = action,
        UserId = actor.UserId,
        Username = actor.Username,
        AtUtc = nowUtc,
        Details = JsonSerializer.Serialize(details, Json)
    };
}
