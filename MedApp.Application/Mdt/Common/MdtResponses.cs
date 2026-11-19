using MedApp.Application.Mdt.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Mdt.Responses;
using MedApp.Domain.Mdt;

namespace MedApp.Application.Mdt.Common;

/// <summary>Items' responses with the lab's work on their pathways (MC21), read in one query.</summary>
public static class MdtResponses
{
    public static async Task<List<MdtItemResponse>> ToResponsesAsync(
        this IMdtItemRepository repository, IReadOnlyList<MdtItem> items, PathwayActor viewer, DateTime nowUtc, CancellationToken ct)
    {
        if (items.Count == 0)
            return [];
        var labWork = await repository.GetLabWorkAsync(items.Select(i => i.PathwayId).Distinct().ToList(), ct);
        return items.Select(i => MdtMapper.ToResponse(i, viewer, nowUtc, labWork)).ToList();
    }

    public static async Task<MdtItemResponse> ToResponseAsync(
        this IMdtItemRepository repository, MdtItem item, PathwayActor viewer, DateTime nowUtc, CancellationToken ct) =>
        (await repository.ToResponsesAsync([item], viewer, nowUtc, ct))[0];
}
