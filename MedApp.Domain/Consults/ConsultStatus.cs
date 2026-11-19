namespace MedApp.Domain.Consults;

/// <summary>Where a consult is (MC25 of <c>docs/multi-clinic-plan.md</c>). Stored as a number: never renumber.</summary>
public enum ConsultStatus
{
    /// <summary>Sent to the asked team, waiting for its reply. The asked team reads the pathway meanwhile.</summary>
    Open = 0,

    /// <summary>The asked team replied; still open (the asked team reads the pathway) until the asking team closes it.</summary>
    Replied = 1,

    /// <summary>Closed by the asking team after the reply. Final: the asked team's reading ended.</summary>
    Closed = 2,

    /// <summary>Withdrawn by the asking team before a reply. Final: the asked team's reading ended.</summary>
    Withdrawn = 3
}
