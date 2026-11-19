using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Consults;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Teams;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>Synthetic teams, people and consults for the consults' tests (MC25).</summary>
public static class ConsultsTestConstants
{
    public const string Question = "Synthetic question: would a punch biopsy do?";
    public const string Reply = "Synthetic reply: yes, a punch biopsy will do.";

    public static readonly Team Dermatology = new() { Id = Guid.Parse("33333333-0000-0000-0000-0000000000c1"), Name = "Dermatology" };
    public static readonly Team Lab = new() { Id = Guid.Parse("33333333-0000-0000-0000-0000000000c2"), Name = "Lab", Kind = TeamKind.Lab };

    private static readonly HashSet<Permission> Works = [Permission.ViewPatients, Permission.WorkPathways];

    /// <summary>Dermatology's clinician, who sent the consults (the pathway's own team).</summary>
    public static readonly PathwayActor Asker = new(
        PathwaysTestConstants.Staff.UserId, PathwaysTestConstants.Staff.Username, Works,
        new TeamScope(PathwaysTestConstants.Staff.UserId, [Dermatology.Id], seesAllTeams: false));

    /// <summary>The lab's pathologist, in the team asked.</summary>
    public static readonly PathwayActor Pathologist = new(
        Guid.Parse("0000000a-0000-0000-0000-0000000000c3"), "test.pathologist", Works,
        new TeamScope(Guid.Parse("0000000a-0000-0000-0000-0000000000c3"), [Lab.Id], seesAllTeams: false));

    /// <summary>An admin: sees every team's, read only, works no pathway.</summary>
    public static readonly PathwayActor AdminViewer = new(
        PathwaysTestConstants.Admin.UserId, PathwaysTestConstants.Admin.Username, new HashSet<Permission> { Permission.ViewPatients },
        new TeamScope(PathwaysTestConstants.Admin.UserId, [], seesAllTeams: true));

    /// <summary>A Dermatology pathway in progress, assigned to <see cref="PathwaysTestConstants.Staff"/>.</summary>
    public static PatientPathway CreateDermatologyPathway(PatientPathwayStatus status = PatientPathwayStatus.InProgress)
    {
        var pathway = PathwaysTestConstants.CreatePathway(status);
        pathway.TeamId = Dermatology.Id;
        return pathway;
    }

    /// <summary>A consult from Dermatology to the lab about <paramref name="pathway"/>, sent by <see cref="Asker"/>.</summary>
    public static Consult CreateConsult(PatientPathway pathway, ConsultStatus status = ConsultStatus.Open)
    {
        var consult = new Consult
        {
            Id = Guid.NewGuid(),
            PatientId = pathway.PatientId,
            Patient = pathway.Patient,
            PathwayId = pathway.Id,
            Pathway = pathway,
            AskingTeamId = Dermatology.Id,
            AskingTeam = Dermatology,
            AskedTeamId = Lab.Id,
            AskedTeam = Lab,
            Question = Question,
            Status = status,
            RequestedByUserId = Asker.UserId,
            RequestedByUsername = Asker.Username,
            RequestedAtUtc = PathwaysTestConstants.Now.AddDays(-1),
            Version = 1,
        };
        if (status is ConsultStatus.Replied or ConsultStatus.Closed)
        {
            consult.Reply = Reply;
            consult.RepliedByUserId = Pathologist.UserId;
            consult.RepliedByUsername = Pathologist.Username;
            consult.RepliedAtUtc = PathwaysTestConstants.Now.AddHours(-2);
            consult.Version++;
        }
        if (status is ConsultStatus.Closed or ConsultStatus.Withdrawn)
        {
            consult.ClosedByUserId = Asker.UserId;
            consult.ClosedByUsername = Asker.Username;
            consult.ClosedAtUtc = PathwaysTestConstants.Now.AddHours(-1);
            consult.Version++;
        }
        return consult;
    }

    /// <summary>The grants an open consult's pathway has: Dermatology's as owner, and the lab's for the consult.</summary>
    public static IEnumerable<PathwayAccess> Grants(PatientPathway pathway, bool consultOpen = true)
    {
        yield return PathwayAccess.Owner(pathway.Id, pathway.PatientId, Dermatology.Id, PathwaysTestConstants.Now.AddDays(-7));
        if (consultOpen)
            yield return PathwayAccess.Consult(pathway.Id, pathway.PatientId, Lab.Id, PathwaysTestConstants.Now.AddDays(-1));
    }
}
