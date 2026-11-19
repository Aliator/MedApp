namespace MedApp.Contracts.Pathways.Insights.Requests;

/// <summary>Query string for <c>GET api/pathways/insights</c>.</summary>
public sealed class GetPathwayInsightsRequest
{
    /// <summary>How many UK weeks (Monday to Sunday) to cover, ending with this one: 1 to 52, 12 by default.</summary>
    public int Weeks { get; set; } = 12;

    /// <summary>Only pathways made from this template. Unset: every pathway.</summary>
    public Guid? TemplateId { get; set; }

    /// <summary>
    /// The clinic picker: only pathways in this team (their own, copied from the template), one of the caller's (any team
    /// for an admin; another is a 400). Unset: every team the caller may see.
    /// </summary>
    public Guid? TeamId { get; set; }
}
