using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Pathways.Timeline.Common;
using MedApp.Application.Referrals.Repositories;
using MedApp.Contracts.Pathways.Timeline.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Pathways.Timeline.Queries.GetPatientTimelineOverview;

/// <summary>What's due for a patient, and their pathways. Opening it is recorded as viewing the patient (once per person every few minutes).</summary>
public sealed class GetPatientTimelineOverviewHandler(
    IPatientRepository patientRepository,
    IPatientPathwayRepository pathwayRepository,
    IPathwayAuditRepository auditRepository,
    IReferralRepository referralRepository,
    IActivityLog activityLog,
    MedApp.Application.Common.Teams.ITeamBoundary boundary,
    MedApp.Application.Teams.Repositories.ITeamRepository teams,
    TimeProvider timeProvider)
    : IRequestHandler<GetPatientTimelineOverviewQuery, PatientTimelineOverviewResponse>
{
    /// <summary>How many due pathways the overview lists; the rest are counted.</summary>
    public const int UpcomingLimit = 5;

    public async Task<PatientTimelineOverviewResponse> Handle(GetPatientTimelineOverviewQuery request, CancellationToken cancellationToken)
    {
        _ = await patientRepository.GetByIdAsync(request.PatientId, cancellationToken)
            ?? throw new NotFoundException($"Patient '{request.PatientId}' was not found.");

        await activityLog.RecordViewsAsync(ActivityAction.PatientViewed, [request.PatientId], cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        // Every team's, then only those the viewer sees in full; the rest are summary lines (the team walls).
        var all = await pathwayRepository.GetByPatientAsync(request.PatientId, cancellationToken);
        var visible = await boundary.VisiblePathwaysAsync(all.Select(p => p.Id), request.Teams, cancellationToken);
        var pathways = all.Where(p => visible.Contains(p.Id)).ToList();
        var others = new List<MedApp.Contracts.Patients.Responses.OtherTeamPathwaySummaryResponse>();
        if (!request.Teams.SeesAllTeams && pathways.Count < all.Count)
        {
            var names = (await teams.GetByIdsAsync(all.Select(p => p.TeamId), cancellationToken)).ToDictionary(t => t.Id, t => t.Name);
            others = OtherTeamPathways.Summarise(all, visible, names, request.Teams);
        }

        // Open or blocked: still to be done, so still due.
        var due = pathways
            .Where(p => !PathwayWorkflow.IsClosed(p.Status))
            .OrderBy(p => p.DueDateUtc)
            .ThenBy(p => p.Id)
            .ToList();

        var listed = pathways
            .OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Select(TimelineMapper.ToPathwayResponse)
            .ToList();

        if (request.FullView && request.Teams.SeesAllTeams)
        {
            var existing = pathways.Select(p => p.Id).ToHashSet();
            var deletions = await auditRepository.GetDeletionsAsync(request.PatientId, cancellationToken);
            listed.AddRange(deletions
                .Where(d => !existing.Contains(d.PatientPathwayId))
                .OrderByDescending(d => d.AtUtc)
                .ThenBy(d => d.Id)
                .DistinctBy(d => d.PatientPathwayId)
                .Select(TimelineMapper.ToDeletedPathwayResponse));
        }

        // Referrals awaiting triage, beside the open pathways: what's coming, oldest first. Only the viewer's teams'
        // (the team walls: another team's referral isn't named, as the light demo has it).
        var awaiting = (await referralRepository.GetAwaitingTriageForPatientAsync(request.PatientId, cancellationToken))
            .Where(r => request.Teams.SeesAllTeams || request.Teams.IsMember(r.TeamId))
            .ToList();

        return new PatientTimelineOverviewResponse
        {
            Upcoming = due.Take(UpcomingLimit).Select(p => TimelineMapper.ToUpcomingResponse(p, now)).ToList(),
            OpenCount = due.Count,
            Pathways = listed,
            AwaitingReferrals = awaiting.Select(TimelineMapper.ToReferralResponse).ToList(),
            OtherTeamPathways = others,
        };
    }
}
