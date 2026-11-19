using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Specimens.Common;
using MedApp.Application.Specimens.Repositories;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Specimens.Responses;
using MedApp.Domain.Specimens;
using MedApp.Domain.Teams;
using MediatR;
using Microsoft.Extensions.Options;

namespace MedApp.Application.Specimens.Commands.RecordSpecimen;

/// <summary>
/// Records a sample taken: 404 for an unknown pathway or stage, or a pathway behind the viewer's team wall (recorded);
/// 409 unless the stage can take one now (<see cref="SpecimenWorkflow.WhyCantTake"/>: the record can change, the
/// pathway is open and the stage is being worked on); 400 for a type or test that's archived or missing, a requesting
/// clinician who can't sign off results, or a taken time in the future or more than 7 days back. A clinic's sample
/// (MC14): its source, its clinic (the pathway's team) and the test asked for, if any. Its number comes from the SQL
/// sequence, and it's saved with its <c>SpecimenTaken</c> audit entry and the lab's grant to see the pathway.
/// </summary>
public sealed class RecordSpecimenHandler(
    IPatientPathwayRepository pathways,
    ISpecimenRepository repository,
    IPermissionHolders permissionHolders,
    IIdentityReadService identity,
    IQrCodeRenderer qrCodes,
    IOptions<SpecimenOptions> options,
    ITeamBoundary boundary,
    ITeamRepository teams,
    IPathwayAccessRepository access,
    TimeProvider timeProvider)
    : IRequestHandler<RecordSpecimenCommand, SpecimenResponse>
{
    public async Task<SpecimenResponse> Handle(RecordSpecimenCommand request, CancellationToken cancellationToken)
    {
        var pathway = await pathways.GetByIdAsync(request.PathwayId, cancellationToken)
                      ?? throw new NotFoundException(PathwayRepositoryExtensions.NotFoundMessage(request.PathwayId));
        // Behind the viewer's team wall: a 404, as one that isn't there (recorded).
        await boundary.EnsurePathwayVisibleAsync(
            pathway, request.Actor.Teams, PathwayRepositoryExtensions.NotFoundMessage(request.PathwayId), cancellationToken);
        var stage = pathway.GetStageOrThrow(request.StageId);

        if (SpecimenWorkflow.WhyCantTake(pathway, stage) is { } why)
            throw new ConflictException(why);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var takenAt = SpecimenRules.StepTime(request.TakenAtUtc, null, now, "taken", nameof(request.TakenAtUtc));

        var (type, labTest) = await SpecimenChoices.CheckAsync(
            repository, permissionHolders, request.SpecimenTypeId, request.LabTestId, request.RequestingUserId, null,
            cancellationToken);

        var sequence = await repository.NextNumberAsync(cancellationToken);
        var specimen = new Specimen
        {
            Id = Guid.NewGuid(),
            Number = SpecimenNumbers.Format(options.Value.NumberPrefix, now, sequence),
            PatientId = pathway.PatientId,
            TeamId = pathway.TeamId,
            Source = SpecimenSource.Clinic,
            RequestingTeamId = pathway.TeamId,
            LabTestId = labTest?.Id,
            PathwayId = pathway.Id,
            StageId = stage.Id,
            SpecimenTypeId = type.Id,
            SpecimenType = type,
            Site = SpecimenRules.Optional(request.Site),
            RequestNotes = SpecimenRules.Optional(request.RequestNotes),
            RequestingUserId = request.RequestingUserId,
            TakenAtUtc = takenAt,
            TakenByUserId = request.Actor.UserId,
            RecordedAtUtc = now,
        };

        var change = new SpecimenChange(specimen, pathway, request.Actor, now);
        change.Take();

        // The sample passes the pathway to the lab (the team walls): it sees it in full from then on, and keeps it.
        if (await teams.GetLabAsync(cancellationToken) is { } lab
            && !await access.HasGrantAsync(pathway.Id, lab.Id, PathwayAccessReason.StageOwner, cancellationToken))
            change.PassToLab(lab.Id);

        await repository.AddAsync(specimen, change.AuditEntries, change.NewGrants, cancellationToken);

        // For the response: the pathway as read (its patient and stages), never saved through the specimen.
        specimen.Patient = pathway.Patient;
        specimen.Pathway = pathway;
        specimen.LabTest = labTest;
        specimen.RequestingTeam = pathway.Team;
        return await repository.ToResponseAsync(specimen, identity, qrCodes, request.Actor, now, cancellationToken);
    }
}
