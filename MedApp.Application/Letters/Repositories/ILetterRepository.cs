using MedApp.Application.Common.Paging;
using MedApp.Application.Letters.Common;
using MedApp.Application.Letters.Queries.SearchLetters;
using MedApp.Domain.Attachments;
using MedApp.Domain.Letters;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Letters.Repositories;

/// <summary>
/// Letters, each loaded with its recipients. Every change saves its audit entries in the same save, so the history
/// can't miss one; a save made from a copy read before someone else's is a 409
/// (<see cref="LetterWorkflow.ChangedMeanwhileMessage"/>).
/// </summary>
public interface ILetterRepository
{
    /// <summary>A letter with its recipients, read-only.</summary>
    Task<Letter?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>The same, tracked, so <see cref="SaveAsync"/> saves changes to it (and to its recipients).</summary>
    Task<Letter?> GetForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>Adds a new letter, its recipients and its audit entries.</summary>
    Task AddAsync(Letter letter, IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct);

    /// <summary>
    /// Saves changes to a letter loaded with <see cref="GetForUpdateAsync"/> (recipients added to or removed from its
    /// list included), with its audit entries.
    /// </summary>
    Task SaveAsync(IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct);

    /// <summary>
    /// Replaces a tracked letter's recipients (loaded with <see cref="GetForUpdateAsync"/>): the old ones are deleted and
    /// the new ones added when it's next saved.
    /// </summary>
    void ReplaceRecipients(Letter letter, IReadOnlyList<LetterRecipient> recipients);

    /// <summary>
    /// Saves an approved letter (loaded with <see cref="GetForUpdateAsync"/>), its PDF's attachment record and their
    /// audit entries, in one save.
    /// </summary>
    Task SaveApprovalAsync(Attachment pdf, IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct);

    /// <summary>A page of letters for a view (see <see cref="LetterSearch"/>), with their patients' names and pathways' titles.</summary>
    Task<PagedResult<LetterRow>> SearchAsync(SearchLettersQuery query, CancellationToken ct);

    /// <summary>The viewer's letters awaiting their approval, and every approved letter not yet sent to everyone.</summary>
    Task<(int ToApprove, int ToSend)> CountAsync(Guid viewerId, CancellationToken ct);

    /// <summary>A pathway's letters, in any status, with their recipients: the stage gate.</summary>
    Task<IReadOnlyList<Letter>> GetForPathwayAsync(Guid pathwayId, CancellationToken ct);

    /// <summary>Letters with these IDs, read-only (for the timeline's full view: their free text).</summary>
    Task<IReadOnlyList<Letter>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>The correction written for a letter, if any.</summary>
    Task<Guid?> GetCorrectionIdAsync(Guid letterId, CancellationToken ct);

    /// <summary>Whether a file is some letter's approved PDF (it can't then be deleted on its own).</summary>
    Task<bool> IsPdfAsync(Guid attachmentId, CancellationToken ct);

    /// <summary>How many letters a pathway has, in any status (it can't be deleted while it has any).</summary>
    Task<int> CountForPathwayAsync(Guid pathwayId, CancellationToken ct);

    /// <summary>When the patient last attended an appointment on the pathway, if ever (a merge field).</summary>
    Task<DateTime?> GetLastAttendedAsync(Guid pathwayId, CancellationToken ct);
}

/// <summary>A letter in a list, with what the list shows about its patient and pathway.</summary>
public sealed record LetterRow(Letter Letter, string PatientFirstName, string PatientLastName, string? PathwayTitle);
