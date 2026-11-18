namespace MedApp.Contracts.Pathways.Insights.Requests;

/// <summary>Query string for <c>GET api/pathways/insights</c>.</summary>
public sealed class GetPathwayInsightsRequest
{
    /// <summary>How many UK weeks (Monday to Sunday) to cover, ending with this one: 1 to 52, 12 by default.</summary>
    public int Weeks { get; set; } = 12;

    /// <summary>Only pathways made from this template. Unset: every pathway.</summary>
    public Guid? TemplateId { get; set; }

    /// <summary>Only pathways in this specialty (their own, copied from the template). Unset: every specialty.</summary>
    public Guid? SpecialtyId { get; set; }
}
