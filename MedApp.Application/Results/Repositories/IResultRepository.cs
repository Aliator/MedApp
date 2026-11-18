using MedApp.Application.Common.Paging;
using MedApp.Application.Results.Queries.SearchResults;
using MedApp.Domain.Attachments;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Results;

namespace MedApp.Application.Results.Repositories;

/// <summary>
/// Results, each loaded with its patient, pathway (and its stages) and specimen (with its type). Every change saves its
/// audit entries in the same save, so the history can't miss one; a save made from a copy read before someone else's is
/// a 409 (<see cref="Common.ResultWorkflow.ChangedMeanwhileMessage"/>). It shares the request's DbContext with the
/// specimen and attachment repositories, so marking a result entered in error saves the result, its specimen and its
/// file's deletion together.
/// </summary>
public interface IResultRepository
{
    /// <summary>A result with its patient, pathway (and stages) and specimen (and type), read-only.</summary>
    Task<Result?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>The same, tracked (its specimen too), so <see cref="SaveAsync"/> saves changes to them.</summary>
    Task<Result?> GetForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>A result's report file, tracked (so marking the result entered in error can delete it), or null.</summary>
    Task<Attachment?> GetReportForUpdateAsync(Guid attachmentId, CancellationToken ct);

    /// <summary>A file, read-only, or null.</summary>
    Task<Attachment?> GetReportAsync(Guid attachmentId, CancellationToken ct);

    /// <summary>Adds a new result and its audit entries (its patient, pathway and specimen aren't added again).</summary>
    Task AddAsync(Result result, IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct);

    /// <summary>Saves changes to what <see cref="GetForUpdateAsync"/> loaded, with its audit entries.</summary>
    Task SaveAsync(IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct);

    /// <summary>A pathway's results awaiting sign-off (awaiting or seen), oldest reported first: the stage gates.</summary>
    Task<IReadOnlyList<Result>> GetAwaitingForPathwayAsync(Guid pathwayId, CancellationToken ct);

    /// <summary>A page of results (see <see cref="ResultSearch"/>), with their patients, pathways and specimens.</summary>
    Task<PagedResult<Result>> SearchAsync(
        SearchResultsQuery query, IReadOnlyCollection<Guid> signOffHolders, CancellationToken ct);

    /// <summary>How many results await this user's sign-off.</summary>
    Task<int> CountAwaitingForAsync(Guid userId, CancellationToken ct);

    /// <summary>Whether a file is the report of a result not entered in error (it can't then be deleted on its own).</summary>
    Task<bool> IsReportAsync(Guid attachmentId, CancellationToken ct);

    /// <summary>How many results a pathway has, in any status (it can't be deleted while it has any).</summary>
    Task<int> CountForPathwayAsync(Guid pathwayId, CancellationToken ct);

    /// <summary>How many results a patient has, in any status (they can't be deleted while they have any).</summary>
    Task<int> CountForPatientAsync(Guid patientId, CancellationToken ct);

    /// <summary>Results with these IDs, read-only (for the timeline's full view: their free text).</summary>
    Task<IReadOnlyList<Result>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
}
