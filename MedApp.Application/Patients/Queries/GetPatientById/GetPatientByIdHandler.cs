using MedApp.Application.Activity.Common;
using MedApp.Application.Patients.Repositories;
using MediatR;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Patients.Responses;
using MedApp.Application.Practices.Repositories;
using MedApp.Domain.Activity;

namespace MedApp.Application.Patients.Queries.GetPatientById;

/// <summary>
/// A patient's record, with their pathways in other teams as summary lines (the team walls). Opening
/// it is recorded in the activity log (once per person every few minutes).
/// </summary>
public sealed class GetPatientByIdHandler(
    IPatientRepository repository,
    IPracticeRepository practices,
    IActivityLog activityLog,
    MedApp.Application.Pathways.Repositories.IPatientPathwayRepository pathways,
    MedApp.Application.Teams.Repositories.ITeamRepository teams,
    MedApp.Application.Common.Teams.ITeamBoundary boundary)
    : IRequestHandler<GetPatientByIdQuery, PatientResponse?>
{
    public async Task<PatientResponse?> Handle(GetPatientByIdQuery request, CancellationToken cancellationToken)
    {
        var patient = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (patient is null)
            return null;

        await activityLog.RecordViewsAsync(ActivityAction.PatientViewed, [patient.Id], cancellationToken);

        var practice = patient.PracticeId is { } practiceId ? await practices.GetAsync(practiceId, cancellationToken) : null;
        return PatientMapper.ToResponse(patient, practice) with
        {
            OtherTeamPathways = await OtherTeamPathwaysAsync(patient.Id, request.Teams, cancellationToken)
        };
    }

    private async Task<List<OtherTeamPathwaySummaryResponse>> OtherTeamPathwaysAsync(
        Guid patientId, MedApp.Application.Common.Teams.TeamScope scope, CancellationToken ct)
    {
        if (scope.SeesAllTeams)
            return [];

        var all = await pathways.GetByPatientAsync(patientId, ct);
        if (all.Count == 0)
            return [];

        var visible = await boundary.VisiblePathwaysAsync(all.Select(p => p.Id), scope, ct);
        var names = (await teams.GetByIdsAsync(all.Select(p => p.TeamId), ct)).ToDictionary(t => t.Id, t => t.Name);
        return MedApp.Application.Pathways.Common.OtherTeamPathways.Summarise(all, visible, names, scope);
    }
}
