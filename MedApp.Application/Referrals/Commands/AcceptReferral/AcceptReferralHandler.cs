using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.PatientPathways.Commands.AssignPatientPathway;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Referrals.Repositories;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Domain.Pathways.PatientPathways;
using MediatR;

namespace MedApp.Application.Referrals.Commands.AcceptReferral;

/// <summary>
/// Accepts a referral into a new pathway. The pathway is made and recorded by the same code as any new pathway
/// (<see cref="PathwayCreation"/>: its Created audit entry, with the referral's ID, its first note and its first
/// stage's visit), and saved in one <c>SaveChangesAsync</c> with the referral's change and its <c>Accepted</c> history
/// entry. Then the person assigned (if any) and whoever registered the referral are told, and the monitor runs.
/// <para>
/// 403 without <c>TriageReferrals</c> or membership of the referral's specialty, or to assign someone without
/// <c>AssignPathways</c>; 409 once it's been triaged (naming who and how), on a restricted or anonymised record, if it
/// changed since the caller's version, or for an archived or empty template; 400 for a template of another specialty.
/// </para>
/// </summary>
public sealed class AcceptReferralHandler(
    IReferralRepository referrals,
    IPatientPathwayRepository pathways,
    IPatientPathwayStagesRepository stagesRepository,
    IIdentityReadService identityReadService,
    IPermissionHolders permissionHolders,
    INotificationSender notificationSender,
    IPathwayMonitor pathwayMonitor,
    TimeProvider timeProvider)
    : IRequestHandler<AcceptReferralCommand, PatientPathwayResponse>
{
    /// <summary>Why someone without <c>AssignPathways</c> can't assign while accepting.</summary>
    public const string CantAssignMessage =
        "Your roles don't let you assign staff to pathways. Accept it without anyone assigned, and someone who can will assign it.";

    public async Task<PatientPathwayResponse> Handle(AcceptReferralCommand request, CancellationToken cancellationToken)
    {
        var actor = request.Actor;
        var referral = await referrals.GetForTriageOrThrowAsync(
            request.ReferralId, actor, request.ExpectedVersion, cancellationToken);

        var template = await stagesRepository.GetPathwayTemplateByIdAsync(request.TemplateId, cancellationToken)
                       ?? throw new NotFoundException($"Template '{request.TemplateId}' was not found.");

        // Only the referral's specialty's templates: another's means redirecting it first.
        if (template.SpecialtyId != referral.SpecialtyId)
            throw new ValidationException([
                new ValidationFailure(
                    nameof(AcceptReferralCommand.TemplateId),
                    ReferralWorkflow.TemplateNotInSpecialtyMessage(template.Name, referral.Specialty?.Name ?? "the referral's specialty"))
            ]);
        if (template.IsArchived)
            throw new ConflictException($"The '{template.Name}' template is archived, so it can't be used for new pathways.");
        if (template.Maps.Count == 0)
            throw new ConflictException($"The '{template.Name}' template has no stages.");

        var assignee = await AssigneeAsync(request, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var priority = PathwayEnums.Parse<PatientPathwayPriority>(request.Priority);
        var pathway = PathwayCreation.FromTemplate(template, referral.PatientId, request.Title, request.DueDate, priority, now);

        var change = new PathwayChange(pathway, actor, now);
        PathwayCreation.Record(change, template.Name, request.DueDate, request.Notes, referral.Id);
        if (assignee is { } chosen)
            change.Assign(chosen.UserId, chosen.Username);

        var referralChange = new ReferralChange(referral, actor, now);
        referralChange.Accept(pathway.Id, template.Name, priority);

        // One save: the referral, its history entry, the new pathway and everything its creation recorded.
        referrals.AddEvents(referralChange.Events);
        try
        {
            await pathways.AddAsync(change, cancellationToken);
        }
        catch (ConflictException)
        {
            // Another triager decided it, or someone changed it, since it was read.
            throw await referrals.TriageConflictAsync(referral.Id, cancellationToken);
        }

        if (assignee is { } assigned && AssignmentNotifications.For(pathway, assigned.UserId, actor, now) is { } assignment)
            await notificationSender.SendAsync(assignment, cancellationToken);
        if (ReferralNotifications.Triaged(referral, actor, now) is { } triaged)
            await notificationSender.SendAsync(triaged, cancellationToken);

        await pathwayMonitor.NotifyAsync(change, cancellationToken);

        var usernames = new Dictionary<Guid, string> { [actor.UserId] = actor.Username };
        if (assignee is { } named)
            usernames[named.UserId] = named.Username;
        var response = PathwayMapper.ToResponse(
            pathway, usernames, now, patient: referral.Patient, viewerId: actor.UserId, viewerPermissions: actor.Permissions);
        response.Referral = ReferralMapper.ToPathwayReferral(referral);
        return response;
    }

    /// <summary>
    /// The person to assign, if any: the actor must hold <c>AssignPathways</c> (403), and the person must exist (404)
    /// and be able to work on pathways (400), as when assigning someone to an existing pathway.
    /// </summary>
    private async Task<(Guid UserId, string Username)?> AssigneeAsync(AcceptReferralCommand request, CancellationToken ct)
    {
        if (request.AssigneeUserId is not { } userId)
            return null;

        if (!request.Actor.Has(Permission.AssignPathways))
            throw new ForbiddenException(CantAssignMessage);

        var usernames = await identityReadService.GetUsernamesByIdAsync([userId], ct);
        if (!usernames.TryGetValue(userId, out var username))
            throw new NotFoundException($"User '{userId}' was not found.");

        var holders = await permissionHolders.WhoHoldsAsync([userId], Permission.WorkPathways, ct);
        if (!holders.Contains(userId))
            throw new ValidationException([
                new ValidationFailure(
                    nameof(AcceptReferralCommand.AssigneeUserId), AssignPatientPathwayHandler.CantWorkOnPathways(username))
            ]);

        return (userId, username);
    }
}
