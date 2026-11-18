using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Specimens.Repositories;
using MedApp.Contracts.Specimens.Responses;
using MedApp.Domain.Specimens;

namespace MedApp.Application.Specimens.Common;

public static class SpecimenRepositoryExtensions
{
    public static async Task<Specimen> GetOrThrowAsync(this ISpecimenRepository repository, Guid id, CancellationToken ct) =>
        await repository.GetByIdAsync(id, ct) ?? throw NotFound(id);

    /// <summary>The specimen, tracked for changes, or a 404.</summary>
    public static async Task<Specimen> GetForUpdateOrThrowAsync(this ISpecimenRepository repository, Guid id, CancellationToken ct) =>
        await repository.GetForUpdateAsync(id, ct) ?? throw NotFound(id);

    /// <summary>
    /// A specimen's page: the usernames it names, the other specimens its stage took and its number as a QR code,
    /// with what <paramref name="viewer"/> may do.
    /// </summary>
    public static async Task<SpecimenResponse> ToResponseAsync(
        this ISpecimenRepository repository,
        Specimen specimen,
        IIdentityReadService identity,
        IQrCodeRenderer qrCodes,
        PathwayActor viewer,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var others = await repository.GetForStageAsync(specimen.StageId, ct);
        var usernames = await identity.GetUsernamesByIdAsync(SpecimenMapper.PeopleIn(specimen), ct);

        // The QR code holds the specimen number only, never anything about the patient (decision 87).
        var label = qrCodes.Render(specimen.Number);

        return SpecimenMapper.ToResponse(specimen, usernames, others, label, viewer, nowUtc);
    }

    public static NotFoundException NotFound(Guid id) => new($"Specimen '{id}' was not found.");
}
