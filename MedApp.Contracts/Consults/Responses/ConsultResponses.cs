using MedApp.Contracts.Pathways.PatientPathways.Responses;

namespace MedApp.Contracts.Consults.Responses;

/// <summary>
/// A consult (MC25): who asked whom about which pathway, the question, the reply once given, and what the signed-in
/// person may do with it.
/// </summary>
public sealed class ConsultResponse
{
    public Guid Id { get; set; }

    /// <summary>Open, Replied, Closed or Withdrawn.</summary>
    public string Status { get; set; } = string.Empty;

    public PathwayPatientResponse? Patient { get; set; }

    public Guid PathwayId { get; set; }
    public string PathwayTitle { get; set; } = string.Empty;

    public Guid AskingTeamId { get; set; }
    public string AskingTeamName { get; set; } = string.Empty;

    public Guid AskedTeamId { get; set; }
    public string AskedTeamName { get; set; } = string.Empty;

    public string Question { get; set; } = string.Empty;
    public string? RequestedByUsername { get; set; }
    public DateTime RequestedAtUtc { get; set; }

    public string? Reply { get; set; }
    public string? RepliedByUsername { get; set; }
    public DateTime? RepliedAtUtc { get; set; }

    /// <summary>Who closed or withdrew it, and when.</summary>
    public string? ClosedByUsername { get; set; }
    public DateTime? ClosedAtUtc { get; set; }

    public int Version { get; set; }

    /// <summary>The asked team's member who works pathways, while it's open without a reply and the record can change.</summary>
    public bool CanReply { get; set; }

    /// <summary>The asking team's member who works pathways, once it's replied.</summary>
    public bool CanClose { get; set; }

    /// <summary>The asking team's member who works pathways, before a reply.</summary>
    public bool CanWithdraw { get; set; }
}

/// <summary>A pathway's consults, newest first, and whether the signed-in person may send another.</summary>
public sealed class PathwayConsultsResponse
{
    public List<ConsultResponse> Consults { get; set; } = [];
    public bool CanRequest { get; set; }
}
