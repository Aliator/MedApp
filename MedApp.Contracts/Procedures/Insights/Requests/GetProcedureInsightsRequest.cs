namespace MedApp.Contracts.Procedures.Insights.Requests;

/// <summary>Query string for <c>GET api/procedures/insights</c>.</summary>
public sealed class GetProcedureInsightsRequest
{
    /// <summary>How many UK weeks (Monday to Sunday) to cover, ending with this one: 1 to 52, 12 by default.</summary>
    public int Weeks { get; set; } = 12;

    /// <summary>Only procedures made from this template. Unset: every procedure.</summary>
    public Guid? TemplateId { get; set; }
}
