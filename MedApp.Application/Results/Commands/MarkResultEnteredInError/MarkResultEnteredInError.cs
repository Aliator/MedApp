using FluentValidation;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Results.Common;
using MedApp.Application.Results.Repositories;
using MedApp.Contracts.Results.Responses;
using MedApp.Domain.Pathways.PatientPathways;
using MediatR;

namespace MedApp.Application.Results.Commands.MarkResultEnteredInError;

/// <summary>Marks a result entered in error (a report on the wrong patient or specimen), with what was wrong.</summary>
public sealed record MarkResultEnteredInErrorCommand(Guid ResultId, string Detail, int ExpectedVersion, PathwayActor Actor)
    : IRequest<ResultResponse>;

public sealed class MarkResultEnteredInErrorValidator : AbstractValidator<MarkResultEnteredInErrorCommand>
{
    public MarkResultEnteredInErrorValidator()
    {
        RuleFor(x => x.ResultId).NotEmpty().WithMessage("Result id is required.");
        RuleFor(x => x.Detail).ErrorDetail();
        RuleFor(x => x.ExpectedVersion).ResultVersion();
    }
}

/// <summary>
/// Marks a result entered in error (decision 96, hazard H-47): 403 unless the caller holds CorrectRecords, or reported
/// it less than 24 hours ago and still holds the permission that let them; 409 if it's already entered in error, the
/// record is anonymised, or it's changed since (allowed while restricted: a correction). The result closes; its report
/// file is soft-deleted (with an <c>AttachmentDeleted</c> entry, decision 71); a specimen's goes back to Received, so the
/// lab can attach the right report. All in one save.
/// </summary>
/// <remarks>
/// The team walls (the light demo's <c>findResult</c>): a result behind the viewer's wall is a 404, recorded; any
/// team that sees it (its own, or one its pathway was passed to, such as the lab) may act on it, by its permissions.
/// </remarks>
public sealed class MarkResultEnteredInErrorHandler(
    IResultRepository repository,
    ITeamBoundary boundary,
    IIdentityReadService identity,
    IPermissionHolders permissionHolders,
    TimeProvider timeProvider)
    : IRequestHandler<MarkResultEnteredInErrorCommand, ResultResponse>
{
    public async Task<ResultResponse> Handle(MarkResultEnteredInErrorCommand request, CancellationToken cancellationToken)
    {
        var result = await repository.GetForUpdateOrThrowAsync(request.ResultId, cancellationToken);
        // Behind the viewer's team wall: a 404, as for one that isn't there (recorded).
        await boundary.EnsureResultVisibleAsync(result, request.Actor.Teams, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (!ResultWorkflow.MayMarkEnteredInError(result, request.Actor, now))
            throw new ForbiddenException(ResultWorkflow.EnteredInErrorNotAllowedMessage);
        if (ResultWorkflow.WhyCantMarkEnteredInError(result) is { } why)
            throw new ConflictException(why);
        ResultWorkflow.EnsureVersion(result, request.ExpectedVersion);

        var change = new ResultChange(result, result.Pathway, request.Actor, now);
        change.MarkEnteredInError(request.Detail.Trim());

        var audit = new List<PatientPathwayAuditEntry>(change.AuditEntries);
        var report = await repository.GetReportForUpdateAsync(result.ReportAttachmentId, cancellationToken);
        if (report is { IsDeleted: false })
        {
            report.DeletedAtUtc = now;
            report.DeletedByUserId = request.Actor.UserId;
            report.DeletedByUsername = request.Actor.Username;

            var pathway = report.PathwayId is { } pathwayId && pathwayId == result.PathwayId ? result.Pathway : null;
            var stage = pathway?.Stages.FirstOrDefault(s => s.Id == report.StageId);
            var target = new AttachmentTarget(result.PatientId, pathway, stage) { Patient = pathway is null ? result.Patient : null };
            audit.Add(AttachmentAudit.Entry(
                report, target, request.Actor, PatientPathwayAuditAction.AttachmentDeleted, now.AddTicks(audit.Count)));
        }

        await repository.SaveAsync(audit, cancellationToken);

        return await repository.ToResponseAsync(result, identity, permissionHolders, request.Actor, now, cancellationToken);
    }
}
