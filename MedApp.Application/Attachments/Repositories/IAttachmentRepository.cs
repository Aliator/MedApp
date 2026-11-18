using MedApp.Application.Attachments.Common;
using MedApp.Application.Attachments.Queries.GetAttachments;
using MedApp.Domain.Attachments;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Attachments.Repositories;

/// <summary>
/// Attached files' metadata (the bytes are in <see cref="IFileStore"/>). Every change saves its audit entry in the
/// same save, so the log can't miss one.
/// </summary>
public interface IAttachmentRepository
{
    /// <summary>Adds a new attachment with the audit entry recording it.</summary>
    Task AddAsync(Attachment attachment, PatientPathwayAuditEntry audit, CancellationToken ct);

    /// <summary>An attachment, deleted or not, read-only.</summary>
    Task<Attachment?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>An attachment, tracked so <see cref="SaveAsync"/> saves changes to it.</summary>
    Task<Attachment?> GetForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>Saves changes to attachments loaded with <see cref="GetForUpdateAsync"/>, with the audit entry recording them.</summary>
    Task SaveAsync(PatientPathwayAuditEntry audit, CancellationToken ct);

    /// <summary>Records an audit entry that changes nothing else (a download).</summary>
    Task AddAuditAsync(PatientPathwayAuditEntry audit, CancellationToken ct);

    /// <summary>The newest attachments matching the query (see <c>AttachmentSearch</c>), and how many match in all.</summary>
    Task<(IReadOnlyList<AttachmentRow> Rows, int TotalCount)> ListAsync(GetAttachmentsQuery query, CancellationToken ct);

    /// <summary>The pathway a stage belongs to; null if there's no such stage.</summary>
    Task<Guid?> GetStagePathwayIdAsync(Guid stageId, CancellationToken ct);

    /// <summary>How many files (not deleted) the patient has, attached to them, their pathways or stages.</summary>
    Task<int> CountForPatientAsync(Guid patientId, CancellationToken ct);

    /// <summary>How many files (not deleted) are attached to the pathway or its stages.</summary>
    Task<int> CountForPathwayAsync(Guid pathwayId, CancellationToken ct);
}
