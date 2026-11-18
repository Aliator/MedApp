namespace MedApp.Application.Common.Identity;

/// <summary>
/// Who holds a permission through their roles: to tell the people who can put something right (a note deletion request
/// to holders of <see cref="Permission.CorrectRecords"/>), or to check that someone can be given a task.
/// </summary>
public interface IPermissionHolders
{
    /// <summary>The IDs of every user who holds this permission, each once.</summary>
    Task<IReadOnlyList<Guid>> GetUserIdsAsync(Permission permission, CancellationToken ct);

    /// <summary>Which of these users hold this permission.</summary>
    Task<IReadOnlySet<Guid>> WhoHoldsAsync(IReadOnlyCollection<Guid> userIds, Permission permission, CancellationToken ct);
}
