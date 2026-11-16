using MedApp.Application.Attachments.Common;
using MedApp.Domain.Attachments;
using MedApp.Domain.Procedures.PatientProcedures;

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
            { ProcedureId: { } procedureId } => attachments.Where(a => a.ProcedureId == procedureId && a.StageId == null),
            { StageId: { } stageId } => attachments.Where(a => a.StageId == stageId),
            _ => attachments.Where(_ => false),
        };

        return query.IncludeDeleted && query.IsAdmin
            ? filtered
            : filtered.Where(a => a.DeletedAtUtc == null);
    }

    /// <summary>
    /// The newest <see cref="AttachmentLimits.MaxListed"/> of them (then by ID, so the order is unique), each with its
    /// procedure's title and stage's name now.
    /// </summary>
    public static IQueryable<AttachmentRow> Newest(
        IQueryable<Attachment> filtered,
        IQueryable<PatientProcedure> procedures,
        IQueryable<PatientProcedureStage> stages) =>
        filtered
            .OrderByDescending(a => a.UploadedAtUtc)
            .ThenByDescending(a => a.Id)
            .Take(AttachmentLimits.MaxListed)
            .Select(a => new AttachmentRow(
                a,
                procedures.Where(p => p.Id == a.ProcedureId).Select(p => p.Title).FirstOrDefault(),
                stages.Where(s => s.Id == a.StageId).Select(s => s.Name).FirstOrDefault()));
}
