using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Lab.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Application.Specimens.Common;
using MedApp.Application.Specimens.Repositories;
using MedApp.Contracts.Specimens.Responses;
using MediatR;

namespace MedApp.Application.Specimens.Commands.EditSpecimen;

/// <summary>
/// Corrects a specimen's details: 409 once it's been sent, while the record is restricted or anonymised, or if it's
/// changed since the caller's version; 400 for an archived type or lab it doesn't already have, a new requesting
/// clinician who can't work on pathways, or a taken time in the future or more than 7 days back. The audit entry
/// names the fields changed, with values only for the structured ones.
/// </summary>
public sealed class EditSpecimenHandler(
    ISpecimenRepository repository,
    IPermissionHolders permissionHolders,
    IIdentityReadService identity,
    IQrCodeRenderer qrCodes,
    ITeamBoundary boundary,
    ITeamRepository teams,
    TimeProvider timeProvider)
    : IRequestHandler<EditSpecimenCommand, SpecimenResponse>
{
    public async Task<SpecimenResponse> Handle(EditSpecimenCommand request, CancellationToken cancellationToken)
    {
        var specimen = await repository.GetForUpdateOrThrowAsync(request.SpecimenId, cancellationToken);
        await boundary.EnsureSpecimenVisibleAsync(
            specimen, request.Actor.Teams, (await teams.GetLabAsync(cancellationToken))?.Id,
            SpecimenRepositoryExtensions.NotFound(specimen.Id).Message, cancellationToken);
        if (SpecimenWorkflow.WhyCantEdit(specimen) is { } why)
            throw new ConflictException(why);
        SpecimenWorkflow.EnsureVersion(specimen, request.ExpectedVersion);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        // An unchanged taken time stays allowed, however long ago it was recorded.
        var takenAt = DateTime.SpecifyKind(request.TakenAtUtc, DateTimeKind.Utc) == specimen.TakenAtUtc
            ? specimen.TakenAtUtc
            : SpecimenRules.StepTime(request.TakenAtUtc, null, now, "taken", nameof(request.TakenAtUtc));

        var (type, labTest) = await SpecimenChoices.CheckAsync(
            repository, permissionHolders, request.SpecimenTypeId, request.LabTestId, request.RequestingUserId, specimen,
            cancellationToken);

        var usernames = specimen.RequestingUserId == request.RequestingUserId
            ? new Dictionary<Guid, string>()
            : await identity.GetUsernamesByIdAsync([specimen.RequestingUserId, request.RequestingUserId], cancellationToken);

        var change = new SpecimenChange(specimen, specimen.Pathway, request.Actor, now);
        if (change.EditDetails(
                type,
                labTest,
                takenAt,
                SpecimenRules.Optional(request.Site),
                SpecimenRules.Optional(request.RequestNotes),
                request.RequestingUserId,
                usernames.GetValueOrDefault(specimen.RequestingUserId),
                usernames.GetValueOrDefault(request.RequestingUserId)))
            await repository.SaveAsync(change.AuditEntries, cancellationToken);

        return await repository.ToResponseAsync(specimen, identity, qrCodes, request.Actor, now, cancellationToken);
    }
}
