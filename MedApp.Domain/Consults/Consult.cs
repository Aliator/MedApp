using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Patients;
using MedApp.Domain.Teams;

namespace MedApp.Domain.Consults;

/// <summary>
/// A consult (MC25 of <c>docs/multi-clinic-plan.md</c>): a pathway's own team (<see cref="AskingTeamId"/>) sends another
/// team (a clinic or the lab, <see cref="AskedTeamId"/>) a question about it. The asked team reads the pathway, read
/// only, while the consult is open (a <see cref="PathwayAccessReason.Consult"/> grant, written and ended with it), and
/// replies; only the asking team closes it after the reply, or withdraws it before one. Nothing is suggested: the team
/// asked and the words are the clinicians'.
/// <para>
/// Patient data: the question and the reply are free text, shown on the pathway's Consults section and the asked
/// team's incoming list, never in the audit log, a notification or a staff timeline. Its history is in the pathway audit
/// log (<c>ConsultRequested</c>, <c>ConsultReplied</c>, <c>ConsultClosed</c>, <c>ConsultWithdrawn</c>).
/// </para>
/// </summary>
public sealed class Consult
{
    public const int MaxQuestionLength = 2000;
    public const int MaxReplyLength = 4000;

    public Guid Id { get; init; }

    public Guid PatientId { get; init; }
    public Patient Patient { get; set; } = null!;

    /// <summary>The pathway it's about. Deleting the pathway deletes its consults (their history stays in the audit log).</summary>
    public Guid PathwayId { get; init; }
    public PatientPathway Pathway { get; set; } = null!;

    /// <summary>The team that sent it: the pathway's own (its team on the team walls).</summary>
    public Guid AskingTeamId { get; init; }
    public Team AskingTeam { get; set; } = null!;

    /// <summary>The team asked: a clinic or the lab.</summary>
    public Guid AskedTeamId { get; init; }
    public Team AskedTeam { get; set; } = null!;

    /// <summary>The question for the asked team (free text).</summary>
    public string Question { get; set; } = string.Empty;

    public ConsultStatus Status { get; set; }

    public Guid RequestedByUserId { get; init; }

    /// <summary>The username at the time, kept if the account is deleted.</summary>
    public string RequestedByUsername { get; init; } = string.Empty;

    public DateTime RequestedAtUtc { get; init; }

    /// <summary>The asked team's reply, as written (free text); null until replied.</summary>
    public string? Reply { get; set; }

    public Guid? RepliedByUserId { get; set; }
    public string? RepliedByUsername { get; set; }
    public DateTime? RepliedAtUtc { get; set; }

    /// <summary>Who closed or withdrew it, and when.</summary>
    public Guid? ClosedByUserId { get; set; }
    public string? ClosedByUsername { get; set; }
    public DateTime? ClosedAtUtc { get; set; }

    /// <summary>1 when sent, one more with each change; a change names the version it started from.</summary>
    public int Version { get; set; }

    /// <summary>Changes on every save, so the later of two overlapping changes is refused (a 409).</summary>
    public byte[] RowVersion { get; set; } = [];

    /// <summary>Open or replied: the asked team reads the pathway.</summary>
    public bool IsOpen => Status is ConsultStatus.Open or ConsultStatus.Replied;
}
