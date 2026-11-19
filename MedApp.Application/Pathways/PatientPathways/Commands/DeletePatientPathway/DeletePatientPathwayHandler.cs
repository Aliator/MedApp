using MedApp.Application.Common.Teams;
using MedApp.Application.Attachments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Consent.Repositories;
using MedApp.Application.Letters.Repositories;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Specimens.Repositories;
using MedApp.Domain.Pathways.PatientPathways;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.DeletePatientPathway;

public sealed class DeletePatientPathwayHandler(
    ITeamBoundary boundary,
    IPatientPathwayRepository repository,
    IAttachmentRepository attachments,
    INoteDeletionRequestRepository noteDeletionRequests,
    ILetterRepository letters,
    ISpecimenRepository specimens,
    MedApp.Application.Results.Repositories.IResultRepository results,
    IConsentRepository consents,
    MedApp.Application.Lab.Repositories.ILabCaseRepository labCases,
    IPathwayMonitor pathwayMonitor,
    TimeProvider timeProvider)
    : IRequestHandler<DeletePatientPathwayCommand>
{
    public const string HasLabCasesMessage = "This pathway has lab cases, so it can't be deleted.";

    public async Task Handle(DeletePatientPathwayCommand request, CancellationToken cancellationToken)
    {
        var pathway = await repository.GetForUpdateOrThrowAsync(request.Id, boundary, request.Actor.Teams, cancellationToken);

        if (!PathwayWorkflow.CanBeDeleted(pathway))
            throw new ConflictException("Only cancelled or archived pathways can be deleted. Cancel it first.");

        // Its files (and its stages') would be left pointing at nothing: they must be deleted first, which is audited.
        var fileCount = await attachments.CountForPathwayAsync(pathway.Id, cancellationToken);
        if (fileCount > 0)
            throw new ConflictException(
                $"This pathway has {fileCount} file{(fileCount == 1 ? "" : "s")} attached, so it can't be deleted. Delete the files first.");

        // Letters are clinical correspondence about it (even a discarded draft is part of its history): kept, so it is.
        var letterCount = await letters.CountForPathwayAsync(pathway.Id, cancellationToken);
        if (letterCount > 0)
            throw new ConflictException(
                $"This pathway has {letterCount} letter{(letterCount == 1 ? "" : "s")}, so it can't be deleted. Archive it instead.");

        // Its specimens are part of the patient's record (and the lab's history): they keep the pathway.
        var specimenCount = await specimens.CountForPathwayAsync(pathway.Id, cancellationToken);
        if (specimenCount > 0)
            throw new ConflictException(
                $"This pathway has {specimenCount} specimen{(specimenCount == 1 ? "" : "s")}, so it can't be deleted.");

        // So are its lab cases (MC15), even one still waiting for its sample.
        if (await labCases.AnyForPathwayAsync(pathway.Id, cancellationToken))
            throw new ConflictException(HasLabCasesMessage);

        var resultCount = await results.CountForPathwayAsync(pathway.Id, cancellationToken);
        if (resultCount > 0)
            throw new ConflictException(
                $"This pathway has {resultCount} result{(resultCount == 1 ? "" : "s")}, so it can't be deleted.");
        // Consent records are part of the patient's record (what they agreed to, and when): they keep the pathway.
        var consentCount = await consents.CountForPathwayAsync(pathway.Id, cancellationToken);
        if (consentCount > 0)
            throw new ConflictException(
                $"This pathway has {consentCount} consent record{(consentCount == 1 ? "" : "s")}, so it can't be deleted.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new PathwayChange(pathway, request.Actor, now);
        change.Audit(PatientPathwayAuditAction.Deleted, new { status = pathway.Status.ToString() });

        // Its notes go with it, so requests to delete them that are still waiting are closed, by the admin, undecided.
        // They share the DbContext, so the deletion's save saves them. Their requesters aren't told.
        foreach (var pending in await noteDeletionRequests.GetPendingForPathwayForUpdateAsync(pathway.Id, cancellationToken))
            pending.Close(NoteDeletionRequestStatus.Closed, request.Actor.UserId, request.Actor.Username, now);

        await repository.DeleteAsync(pathway, change.AuditEntries, cancellationToken);

        // No rule watches for deletions yet; every pathway command still asks, so a new event needs no handler change.
        await pathwayMonitor.NotifyAsync(change, cancellationToken);
    }
}
