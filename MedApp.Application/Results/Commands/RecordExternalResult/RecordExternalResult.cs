using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Results.Common;
using MedApp.Application.Results.Repositories;
using MedApp.Contracts.Results.Responses;
using MedApp.Domain.Results;
using MediatR;

namespace MedApp.Application.Results.Commands.RecordExternalResult;

/// <summary>
/// Marks an attached file as a result from elsewhere (a scan report from another service): <paramref name="Title"/> says
/// what it is, <paramref name="PathwayId"/> the patient's pathway (left out, the file's own), and
/// <paramref name="ResponsibleUserId"/> who must sign it off.
/// </summary>
public sealed record RecordExternalResultCommand(
    Guid AttachmentId,
    string Title,
    Guid? PathwayId,
    Guid ResponsibleUserId,
    PathwayActor Actor)
    : IRequest<ResultResponse>;

public sealed class RecordExternalResultValidator : AbstractValidator<RecordExternalResultCommand>
{
    public RecordExternalResultValidator()
    {
        RuleFor(x => x.AttachmentId).NotEmpty().WithMessage("Choose the file that is the report.");
        RuleFor(x => x.Title).Title();
        RuleFor(x => x.ResponsibleUserId).NotEmpty().WithMessage("Choose the clinician who will sign it off.");
    }
}

/// <summary>
/// Records an external result (decision 91; holders of WorkPathways, checked by the endpoint). The file must be live and
/// on the patient, one of their pathways or a stage (not a referral's letter: 400), and not already a result's report
/// (409). The pathway, if given, must be the patient's and match the file's own (400). The responsible clinician must
/// hold SignOffResults (400). A restricted or anonymised record's can't be recorded (409: new work, decision 75). The
/// result and its <c>ResultRecorded</c> entry are one save; the responsible clinician is told (<c>ResultReady</c>)
/// unless they recorded it.
/// <para>
/// The team walls: a file behind the viewer's wall is a 404 (recorded), as for one that isn't there; a pathway behind it
/// is the same 400 as one that isn't the patient's (recorded); a pathway the viewer sees but another team owns is a 409
/// (<see cref="TeamWalls.NotYourTeams"/>), and so is a file with no pathway that another team owns
/// (<see cref="TeamWalls.RecordNotYours"/>): a result belongs to its pathway's team, or its file's.
/// </para>
/// </summary>
public sealed class RecordExternalResultHandler(
    IResultRepository repository,
    ITeamBoundary boundary,
    IPatientRepository patients,
    IPatientPathwayRepository pathways,
    IIdentityReadService identity,
    IPermissionHolders permissionHolders,
    INotificationSender notifications,
    TimeProvider timeProvider)
    : IRequestHandler<RecordExternalResultCommand, ResultResponse>
{
    public const string ReferralFileMessage =
        "A referral's letter can't be a result. Attach the report to the patient or one of their pathways.";

    public const string AlreadyResultMessage = "This file is already a result's report.";

    public const string NotPatientsPathwayMessage = "Choose one of this patient's pathways.";

    public const string PathwayMismatchMessage = "The file is attached to another pathway: choose that one, or leave it out.";

    public async Task<ResultResponse> Handle(RecordExternalResultCommand request, CancellationToken cancellationToken)
    {
        var file = await repository.GetReportAsync(request.AttachmentId, cancellationToken);
        if (file is null || file.IsDeleted)
            throw new NotFoundException(AttachmentWalls.NotFoundMessage(request.AttachmentId));
        var scope = request.Actor.Teams;
        await AttachmentWalls.EnsureVisibleAsync(file, boundary, scope, cancellationToken);
        if (file.ReferralId is not null)
            throw Invalid(nameof(request.AttachmentId), ReferralFileMessage);

        var patient = await patients.GetByIdAsync(file.PatientId, cancellationToken)
                      ?? throw new NotFoundException($"Patient '{file.PatientId}' was not found.");
        if (PatientRules.IsRestricted(patient) && !PatientRules.IsAnonymised(patient))
            throw new ConflictException(ResultWorkflow.RestrictedRecordMessage);
        PatientRules.EnsureCanChange(patient);

        if (file.PathwayId is { } filePathway && request.PathwayId is { } asked && asked != filePathway)
            throw Invalid(nameof(request.PathwayId), PathwayMismatchMessage);
        var pathwayId = file.PathwayId ?? request.PathwayId;
        var pathway = pathwayId is { } id ? await pathways.GetByIdAsync(id, cancellationToken) : null;
        if (pathwayId is not null && (pathway is null || pathway.PatientId != file.PatientId))
            throw Invalid(nameof(request.PathwayId), NotPatientsPathwayMessage);
        if (pathway is not null)
        {
            // Behind the viewer's wall: the same 400 as a pathway that isn't the patient's, so it isn't confirmed.
            if (await boundary.GetPathwayAccessAsync(pathway.Id, scope, cancellationToken) is null)
            {
                await boundary.RefuseAsync(TeamWalls.PathwayTarget, pathway.Id, NotPatientsPathwayMessage, cancellationToken);
                throw Invalid(nameof(request.PathwayId), NotPatientsPathwayMessage);
            }
            await boundary.EnsureCanChangePathwayAsync(
                pathway, scope, PathwayRepositoryExtensions.NotFoundMessage(pathway.Id), cancellationToken);
        }
        else
            await boundary.EnsureCanChangeTeamAsync(file.TeamId, scope, "This file", cancellationToken);

        await permissionHolders.EnsureCanBeResponsibleAsync(
            request.ResponsibleUserId, nameof(request.ResponsibleUserId), cancellationToken);

        if (await repository.IsReportAsync(file.Id, cancellationToken))
            throw new ConflictException(AlreadyResultMessage);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var result = new Result
        {
            Id = Guid.NewGuid(),
            PatientId = file.PatientId,
            // Its pathway's team, else the file's (the team walls).
            TeamId = pathway?.TeamId ?? file.TeamId,
            PathwayId = pathway?.Id,
            Source = ResultSource.ExternalReport,
            Title = request.Title.Trim(),
            ReportAttachmentId = file.Id,
            ReportedAtUtc = now,
            ReportedByUserId = request.Actor.UserId,
            ResponsibleUserId = request.ResponsibleUserId,
        };
        var responsible = await identity.GetUsernamesByIdAsync([request.ResponsibleUserId], cancellationToken);
        var change = new ResultChange(result, pathway, request.Actor, now);
        change.Record(responsible.GetValueOrDefault(request.ResponsibleUserId, PathwayMapper.DeletedUser));
        await repository.AddAsync(result, change.AuditEntries, cancellationToken);

        if (ResultNotifications.Ready(result, request.Actor, now) is { } notification)
            await notifications.SendAsync(notification, cancellationToken);

        var saved = await repository.GetOrThrowAsync(result.Id, cancellationToken);
        return await repository.ToResponseAsync(saved, identity, permissionHolders, request.Actor, now, cancellationToken);
    }

    private static ValidationException Invalid(string field, string message) =>
        new([new ValidationFailure(field, message)]);
}
