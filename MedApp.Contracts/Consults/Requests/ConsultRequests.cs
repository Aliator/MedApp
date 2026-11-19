namespace MedApp.Contracts.Consults.Requests;

/// <summary>Sends a consult: the pathway, the team asked (a clinic or the lab) and the question.</summary>
public sealed class RequestConsultRequest
{
    public Guid PathwayId { get; set; }
    public Guid AskedTeamId { get; set; }
    public string Question { get; set; } = string.Empty;
}

/// <summary>The asked team's reply, from the version the caller saw.</summary>
public sealed class ReplyToConsultRequest
{
    public string Reply { get; set; } = string.Empty;
    public int ExpectedVersion { get; set; }
}

/// <summary>Closes or withdraws a consult, from the version the caller saw.</summary>
public sealed class ChangeConsultRequest
{
    public int ExpectedVersion { get; set; }
}
