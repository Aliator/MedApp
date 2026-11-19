using System.Text.Json;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Consults;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Teams;

namespace MedApp.Application.Consults.Common;

/// <summary>
/// Applies one request's change to a consult, writes its audit entry (<see cref="PatientPathwayAuditEntry"/>, actions 59
/// to 62) and the asked team's grant (<see cref="PathwayAccess"/>, <see cref="PathwayAccessReason.Consult"/>): written
/// when it's sent, ended when it's closed or withdrawn, all saved in the same <c>SaveChangesAsync</c>
/// (<see cref="Repositories.IConsultRepository"/>), so neither the history nor the walls can miss one. It moves the
/// consult's <see cref="Consult.Version"/> on once per change. Handlers check first that the change is allowed
/// (<see cref="ConsultRules"/>); this only applies it.
/// <para>
/// Not a change to the pathway, so it doesn't go through <see cref="PathwayChange"/> or move the pathway's LastUpdated,
/// as consent doesn't. Details hold the consult's ID and the teams by ID and admin-written name; <b>never the question or
/// the reply</b> (DPIA risk 47).
/// </para>
/// </summary>
public sealed class ConsultChange(
    Consult consult,
    PatientPathway pathway,
    string askingTeamName,
    string askedTeamName,
    PathwayActor actor,
    DateTime nowUtc)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly List<PatientPathwayAuditEntry> _auditEntries = [];

    public Consult Consult => consult;

    public PathwayActor Actor => actor;

    public DateTime NowUtc => nowUtc;

    public IReadOnlyList<PatientPathwayAuditEntry> AuditEntries => _auditEntries;

    /// <summary>The asked team's grant to write with a new consult; null for any other change.</summary>
    public PathwayAccess? NewGrant { get; private set; }

    /// <summary>Whether the asked team's grant ends with this change (closed or withdrawn).</summary>
    public bool EndsGrant { get; private set; }

    /// <summary>A new consult, sent: Open at version 1, and the asked team's grant from now.</summary>
    public void Requested()
    {
        consult.Status = ConsultStatus.Open;
        consult.Version = 0;
        NewGrant = PathwayAccess.Consult(consult.PathwayId, consult.PatientId, consult.AskedTeamId, nowUtc);
        Audit(PatientPathwayAuditAction.ConsultRequested);
    }

    public void Replied(string reply)
    {
        consult.Status = ConsultStatus.Replied;
        consult.Reply = reply;
        consult.RepliedByUserId = actor.UserId;
        consult.RepliedByUsername = actor.Username;
        consult.RepliedAtUtc = nowUtc;
        Audit(PatientPathwayAuditAction.ConsultReplied);
    }

    public void Closed()
    {
        Close(ConsultStatus.Closed);
        Audit(PatientPathwayAuditAction.ConsultClosed);
    }

    public void Withdrawn()
    {
        Close(ConsultStatus.Withdrawn);
        Audit(PatientPathwayAuditAction.ConsultWithdrawn);
    }

    private void Close(ConsultStatus status)
    {
        consult.Status = status;
        consult.ClosedByUserId = actor.UserId;
        consult.ClosedByUsername = actor.Username;
        consult.ClosedAtUtc = nowUtc;
        EndsGrant = true;
    }

    private void Audit(PatientPathwayAuditAction action)
    {
        consult.Version++;
        _auditEntries.Add(new PatientPathwayAuditEntry
        {
            Id = Guid.NewGuid(),
            PatientPathwayId = pathway.Id,
            PathwayTitle = pathway.Title,
            PatientId = consult.PatientId,
            // The asking team's (the pathway's): the asked team's timeline shows it only while it reads the pathway.
            TeamId = consult.AskingTeamId,
            Action = action,
            UserId = actor.UserId,
            Username = actor.Username,
            AtUtc = nowUtc.AddTicks(_auditEntries.Count),
            Details = JsonSerializer.Serialize(new
            {
                consultId = consult.Id,
                askingTeamId = consult.AskingTeamId,
                askingTeam = askingTeamName,
                askedTeamId = consult.AskedTeamId,
                askedTeam = askedTeamName,
            }, Json),
        });
    }
}
