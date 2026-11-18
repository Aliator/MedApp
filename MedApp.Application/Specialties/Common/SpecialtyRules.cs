using MedApp.Application.Common.Exceptions;
using MedApp.Application.Specialties.Repositories;
using MedApp.Domain.Specialties;

namespace MedApp.Application.Specialties.Common;

/// <summary>The rules and messages for specialties, shared by their handlers and by those that choose one.</summary>
public static class SpecialtyRules
{
    public const string ChangedMeanwhileMessage =
        "Someone changed this specialty while you had it open. Reload to see the change, then try again.";

    public static string NameTakenMessage(string name) => $"A specialty called '{name}' already exists.";

    public static string InUseMessage(Specialty specialty, SpecialtyUsage usage) =>
        $"The '{specialty.Name}' specialty has {Describe(usage)}, so it can't be deleted. Archive it instead.";

    public static async Task<Specialty> GetOrThrowAsync(this ISpecialtyRepository repository, Guid id, CancellationToken ct) =>
        await repository.GetAsync(id, ct) ?? throw new NotFoundException($"Specialty '{id}' was not found.");

    /// <summary>A change must start from the version saved now, or someone else has changed it since (409).</summary>
    public static void EnsureVersion(Specialty specialty, int expectedVersion)
    {
        if (specialty.Version != expectedVersion)
            throw new ConflictException(ChangedMeanwhileMessage);
    }

    /// <summary>
    /// Checks specialties being given to something (a template, a member of staff): every one must exist (404), and
    /// an archived one may only stay where it's already given (409). Returns them by ID.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, Specialty>> CheckChosenAsync(
        ISpecialtyRepository repository,
        IReadOnlyCollection<Guid> ids,
        IReadOnlySet<Guid> alreadyGiven,
        CancellationToken ct)
    {
        var distinct = ids.Distinct().ToList();
        var found = (await repository.GetByIdsAsync(distinct, ct)).ToDictionary(s => s.Id);

        var missing = distinct.Where(id => !found.ContainsKey(id)).ToList();
        if (missing.Count != 0)
            throw new NotFoundException($"Specialty not found: {string.Join(", ", missing)}.");

        var archived = distinct
            .Where(id => found[id].IsArchived && !alreadyGiven.Contains(id))
            .Select(id => found[id].Name)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (archived.Count != 0)
            throw new ConflictException(
                $"Archived specialties can't be chosen: {string.Join(", ", archived)}. Restore it on the Specialties page first.");

        return found;
    }

    private static string Describe(SpecialtyUsage usage)
    {
        var parts = new List<string>();
        if (usage.TemplateCount > 0)
            parts.Add(Count(usage.TemplateCount, "pathway template", "pathway templates"));
        if (usage.PathwayCount > 0)
            parts.Add(Count(usage.PathwayCount, "pathway", "pathways"));
        if (usage.MemberCount > 0)
            parts.Add(Count(usage.MemberCount, "member of staff", "members of staff"));
        return parts.Count switch
        {
            1 => parts[0],
            2 => $"{parts[0]} and {parts[1]}",
            _ => $"{parts[0]}, {parts[1]} and {parts[2]}"
        };
    }

    private static string Count(int count, string one, string many) => count == 1 ? $"1 {one}" : $"{count} {many}";
}
