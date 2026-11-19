using FluentValidation;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Specimens.Common;
using MedApp.Application.Specimens.Repositories;
using MedApp.Contracts.Specimens.Responses;
using MedApp.Domain.Activity;
using MedApp.Domain.Specimens;
using MedApp.Application.Common.Teams;
using MedApp.Application.Lab.Common;
using MedApp.Application.Teams.Repositories;
using MediatR;

namespace MedApp.Application.Specimens.Queries.GetSpecimen;

/// <summary>One specimen, with its label data and what <paramref name="Viewer"/> may do with it.</summary>
public sealed record GetSpecimenQuery(Guid SpecimenId, PathwayActor Viewer) : IRequest<SpecimenResponse>;

/// <summary>The specimen with this number (typed, or scanned from its label), with what <paramref name="Viewer"/> may do.</summary>
public sealed record FindSpecimenQuery(string Number, PathwayActor Viewer) : IRequest<SpecimenResponse>;

public sealed class FindSpecimenValidator : AbstractValidator<FindSpecimenQuery>
{
    public FindSpecimenValidator()
    {
        RuleFor(x => x.Number)
            .Must(n => SpecimenNumbers.Normalise(n) is not null)
            .WithMessage($"Type or scan the sample number (at most {Specimen.MaxNumberLength} characters).");
    }
}

/// <summary>
/// One specimen. Opening it (its page, label or request sheet) is recorded against its patient as
/// <see cref="ActivityAction.SpecimenViewed"/>, with the specimen's ID in the details (once per person and specimen
/// every few minutes); the log fails closed.
/// </summary>
public sealed class GetSpecimenHandler(
    ISpecimenRepository repository,
    IActivityLog activityLog,
    IIdentityReadService identity,
    IQrCodeRenderer qrCodes,
    ITeamBoundary boundary,
    ITeamRepository teams,
    TimeProvider timeProvider)
    : IRequestHandler<GetSpecimenQuery, SpecimenResponse>, IRequestHandler<FindSpecimenQuery, SpecimenResponse>
{
    public const string NotFoundByNumberMessage = "No sample has that number. Check it, or scan the label again.";

    public async Task<SpecimenResponse> Handle(GetSpecimenQuery request, CancellationToken cancellationToken) =>
        await ViewAsync(
            await repository.GetOrThrowAsync(request.SpecimenId, cancellationToken), request.Viewer,
            SpecimenRepositoryExtensions.NotFound(request.SpecimenId).Message, cancellationToken);

    /// <summary>The number is in the body, never the URL, so it stays out of request logs. Unknown is a 404.</summary>
    public async Task<SpecimenResponse> Handle(FindSpecimenQuery request, CancellationToken cancellationToken)
    {
        var number = SpecimenNumbers.Normalise(request.Number)!;
        var specimen = await repository.GetByNumberAsync(number, cancellationToken)
                       ?? throw new NotFoundException(NotFoundByNumberMessage);
        // Another team's (or the hospital's) is a 404 as one that doesn't exist: the same words.
        return await ViewAsync(specimen, request.Viewer, NotFoundByNumberMessage, cancellationToken);
    }

    private async Task<SpecimenResponse> ViewAsync(Specimen specimen, PathwayActor viewer, string notFoundMessage, CancellationToken ct)
    {
        // The team walls (MC14): the lab and admins see every sample, a clinic its own; else the recorded 404.
        var labTeamId = (await teams.GetLabAsync(ct))?.Id;
        var access = await boundary.EnsureSpecimenVisibleAsync(specimen, viewer.Teams, labTeamId, notFoundMessage, ct);

        await activityLog.RecordViewAsync(
            ActivityAction.SpecimenViewed, specimen.PatientId, new { specimenId = specimen.Id, access = access.ToString() }, ct);

        return await repository.ToResponseAsync(
            specimen, identity, qrCodes, viewer, timeProvider.GetUtcNow().UtcDateTime, ct,
            canOpenLabCase: LabSamples.CanOpenCaseFor(specimen, viewer, labTeamId));
    }
}
