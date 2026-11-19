using MedApp.Application.Common.Teams;

namespace MedApp.Application.Pathways.Insights.Common;

/// <summary>
/// Which pathways the insights count: every one, or only those made from one template, or in one team (the
/// pathway's own, copied from its template when it was made), or both; and only those in <see cref="TeamIds"/> when it's
/// given (the viewer's teams, the team walls: null for a viewer who sees every team). Counts read from the audit log are
/// then matched to existing pathways, so deleted ones drop out (see <see cref="PathwayInsightsQueries.InScope(IQueryable{Domain.Pathways.PatientPathways.PatientPathwayAuditEntry}, IQueryable{Domain.Pathways.PatientPathways.PatientPathway}, InsightsScope)"/>).
/// </summary>
public sealed record InsightsScope(Guid? TemplateId = null, Guid? TeamId = null, IReadOnlyCollection<Guid>? TeamIds = null)
{
    /// <summary>Every pathway.</summary>
    public static readonly InsightsScope Everything = new();

    /// <summary>Whether it narrows the pathways at all.</summary>
    public bool IsNarrowed => TemplateId is not null || TeamId is not null || TeamIds is not null;

    /// <summary>
    /// The teams Insights counts for a viewer (the light demo's <c>insightsTeams</c>): every team for one who sees every
    /// team (admins, and the system), else only their own (none, if they're in none).
    /// </summary>
    public static IReadOnlyCollection<Guid>? TeamsOf(TeamScope viewer) =>
        viewer.SeesAllTeams ? null : viewer.TeamIds.ToList();
}
