namespace MedApp.Application.Pathways.Insights.Common;

/// <summary>
/// Which pathways the insights count: every one, or only those made from one template, or in one team (the
/// pathway's own, copied from its template when it was made), or both. Counts read from the audit log are then
/// matched to existing pathways, so deleted ones drop out (see <see cref="PathwayInsightsQueries.InScope(IQueryable{Domain.Pathways.PatientPathways.PatientPathwayAuditEntry}, IQueryable{Domain.Pathways.PatientPathways.PatientPathway}, InsightsScope)"/>).
/// </summary>
public sealed record InsightsScope(Guid? TemplateId = null, Guid? TeamId = null)
{
    /// <summary>Every pathway.</summary>
    public static readonly InsightsScope Everything = new();

    /// <summary>Whether it narrows the pathways at all.</summary>
    public bool IsNarrowed => TemplateId is not null || TeamId is not null;
}
