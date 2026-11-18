using MedApp.Domain.Attachments;
using MedApp.Domain.Consent;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Consent.Repositories;

/// <summary>
/// Consent records, each loaded with its patient, pathway (and its stages) and form, and the consent forms list. Every
/// change saves its audit entries in the same save; a save made from a copy read before someone else's is a 409
/// (<see cref="Common.ConsentWorkflow.ChangedMeanwhileMessage"/>). Shares the request's DbContext with the attachment
/// repository, so a scan's soft delete is saved with the record.
/// </summary>
public interface IConsentRepository
{
    /// <summary>A record with its patient, pathway (and stages) and form, tracked, so <see cref="SaveAsync"/> saves changes to it.</summary>
    Task<ConsentRecord?> GetForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>A pathway's records, in any status, oldest recorded first, with the patient, pathway (and stages) and form; read-only.</summary>
    Task<IReadOnlyList<ConsentRecord>> GetForPathwayAsync(Guid pathwayId, CancellationToken ct);

    /// <summary>A stage's records, in any status (the stage gate looks for a Valid one).</summary>
    Task<IReadOnlyList<ConsentRecord>> GetForStageAsync(Guid stageId, CancellationToken ct);

    /// <summary>Adds a new record and its audit entries.</summary>
    Task AddAsync(ConsentRecord record, IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct);

    /// <summary>
    /// Saves changes to a record loaded with <see cref="GetForUpdateAsync"/>, with its audit entries, and a scan it soft
    /// deleted (tracked by the same DbContext), if any.
    /// </summary>
    Task SaveAsync(IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct);

    /// <summary>The attachment, tracked (for a scan to check, or to soft-delete with the record's correction).</summary>
    Task<Attachment?> GetAttachmentForUpdateAsync(Guid attachmentId, CancellationToken ct);

    /// <summary>Whether a file is some consent record's scan (it can't then be deleted on its own).</summary>
    Task<bool> IsScanAsync(Guid attachmentId, CancellationToken ct);

    /// <summary>How many consent records a pathway has, in any status (it can't be deleted while it has any).</summary>
    Task<int> CountForPathwayAsync(Guid pathwayId, CancellationToken ct);

    /// <summary>Records with these IDs, read-only (for the timeline's full view: their free text).</summary>
    Task<IReadOnlyList<ConsentRecord>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    // Consent forms.

    Task<IReadOnlyList<ConsentForm>> GetFormsAsync(bool includeArchived, CancellationToken ct);

    Task<ConsentForm?> GetFormAsync(Guid id, CancellationToken ct);

    /// <summary>Whether another form has this name and version (ignoring case).</summary>
    Task<bool> FormExistsAsync(string name, string formVersion, Guid? exceptId, CancellationToken ct);

    /// <summary>Which of these forms some record has used (so they're archived, not deleted).</summary>
    Task<IReadOnlySet<Guid>> GetFormsInUseAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    Task AddFormAsync(ConsentForm form, CancellationToken ct);

    /// <summary>Saves a form changed after <see cref="GetFormAsync"/>.</summary>
    Task UpdateFormAsync(ConsentForm form, CancellationToken ct);

    Task DeleteFormAsync(Guid id, CancellationToken ct);
}
