using MedApp.Application.Attachments.Common;
using MedApp.Domain.Attachments;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Attachments.Queries.GetAttachments;

/// <summary>
/// The query behind <see cref="GetAttachmentsQuery"/>, as plain LINQ that EF translates, so the same code runs in SQL
/// and in unit tests.
/// </summary>
public static class AttachmentSearch
{
    /// <summary>The attachments the query asks for, in no order.</summary>
    public static IQueryable<Attachment> Filter(IQueryable<Attachment> attachments, GetAttachmentsQuery query)
    {
        var filtered = query switch
        {
            { PatientId: { } patientId } => attachments.Where(a => a.PatientId == patientId),
            { PathwayId: { } pathwayId } => attachments.Where(a => a.PathwayId == pathwayId && a.StageId == null),
            { StageId: { } stageId } => attachments.Where(a => a.StageId == stageId),
            { ReferralId: { } referralId } => attachments.Where(a => a.ReferralId == referralId),
            _ => attachments.Where(_ => false),
        };

        return query.IncludeDeleted && query.CanCorrectRecords
            ? filtered
            : filtered.Where(a => a.DeletedAtUtc == null);
    }

    /// <summary>
    /// The newest <see cref="AttachmentLimits.MaxListed"/> of them (then by ID, so the order is unique), each with its
    /// pathway's title and stage's name now.
    /// </summary>
    public static IQueryable<AttachmentRow> Newest(
        IQueryable<Attachment> filtered,
        IQueryable<PatientPathway> pathways,
        IQueryable<PatientPathwayStage> stages) =>
        filtered
            .OrderByDescending(a => a.UploadedAtUtc)
            .ThenByDescending(a => a.Id)
            .Take(AttachmentLimits.MaxListed)
            .Select(a => new AttachmentRow(
                a,
                pathways.Where(p => p.Id == a.PathwayId).Select(p => p.Title).FirstOrDefault(),
                stages.Where(s => s.Id == a.StageId).Select(s => s.Name).FirstOrDefault()));
}
