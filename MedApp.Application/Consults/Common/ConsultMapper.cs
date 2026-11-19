using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Consults.Responses;
using MedApp.Domain.Consults;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Consults.Common;

/// <summary>Consults as the API returns them, with what the viewer may do with each (the light demo's <c>consultResponse</c>).</summary>
public static class ConsultMapper
{
    /// <summary>
    /// One consult, read with its patient, pathway and teams. Replying is the asked team's, closing and withdrawing the
    /// asking team's, each for those who work pathways (<see cref="Permission.WorkPathways"/>).
    /// </summary>
    public static ConsultResponse ToResponse(Consult consult, PathwayActor viewer)
    {
        var works = viewer.Has(Permission.WorkPathways);
        var asked = works && viewer.Teams.IsMember(consult.AskedTeamId);
        var asking = works && viewer.Teams.IsMember(consult.AskingTeamId);
        return new ConsultResponse
        {
            Id = consult.Id,
            Status = consult.Status.ToString(),
            Patient = PathwayMapper.ToPatient(consult.Patient),
            PathwayId = consult.PathwayId,
            PathwayTitle = consult.Pathway?.Title ?? string.Empty,
            AskingTeamId = consult.AskingTeamId,
            AskingTeamName = consult.AskingTeam?.Name ?? string.Empty,
            AskedTeamId = consult.AskedTeamId,
            AskedTeamName = consult.AskedTeam?.Name ?? string.Empty,
            Question = consult.Question,
            RequestedByUsername = consult.RequestedByUsername,
            RequestedAtUtc = consult.RequestedAtUtc,
            Reply = consult.Reply,
            RepliedByUsername = consult.RepliedByUsername,
            RepliedAtUtc = consult.RepliedAtUtc,
            ClosedByUsername = consult.ClosedByUsername,
            ClosedAtUtc = consult.ClosedAtUtc,
            Version = consult.Version,
            CanReply = asked && consult.Status == ConsultStatus.Open && PatientRules.CanChange(consult.Patient),
            CanClose = asking && consult.Status == ConsultStatus.Replied,
            CanWithdraw = asking && consult.Status == ConsultStatus.Open,
        };
    }

    /// <summary>
    /// Whether the viewer may send a consult about a pathway (loaded with its patient): its own team's member who works
    /// pathways, while it's open and the record can change.
    /// </summary>
    public static bool CanRequest(PatientPathway pathway, PathwayActor viewer) =>
        viewer.Has(Permission.WorkPathways)
        && viewer.Teams.IsMember(pathway.TeamId)
        && !ConsultRules.ClosedPathway.Contains(pathway.Status)
        && PatientRules.CanChange(pathway.Patient);
}
