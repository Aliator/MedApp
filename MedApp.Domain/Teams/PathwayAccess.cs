namespace MedApp.Domain.Teams;

/// <summary>
/// A grant to see a pathway in full (the team walls, decision 201 of <c>docs/multi-clinic-plan.md</c>): to a team
/// (<see cref="TeamId"/>) or one person (<see cref="UserId"/>), for a <see cref="Reason"/>, from
/// <see cref="StartedAtUtc"/> until <see cref="EndedAtUtc"/> (null: still live). Written only in the same save as the
/// event that causes it (a pathway made, passed to the lab, a consult or an MDT listing), and never edited by hand.
/// No foreign key to the pathway, so the history of who could see it outlives its deletion.
/// </summary>
public sealed class PathwayAccess
{
    public Guid Id { get; init; }

    public Guid PathwayId { get; init; }

    /// <summary>The pathway's patient, so a patient's grants are found without the pathway (export, anonymisation).</summary>
    public Guid PatientId { get; init; }

    /// <summary>The team granted it, or null for a grant to one person.</summary>
    public Guid? TeamId { get; init; }

    /// <summary>The person granted it, or null for a grant to a team.</summary>
    public Guid? UserId { get; init; }

    public PathwayAccessReason Reason { get; init; }

    public DateTime StartedAtUtc { get; init; }

    /// <summary>When it ended, or null while it's live. Owner and stage owner grants never end.</summary>
    public DateTime? EndedAtUtc { get; set; }

    /// <summary>The pathway's own team's grant, written with the pathway.</summary>
    public static PathwayAccess Owner(Guid pathwayId, Guid patientId, Guid teamId, DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        PathwayId = pathwayId,
        PatientId = patientId,
        TeamId = teamId,
        Reason = PathwayAccessReason.Owner,
        StartedAtUtc = nowUtc,
    };

    /// <summary>A stage owner's grant (the lab's, once the pathway is passed to it).</summary>
    public static PathwayAccess StageOwner(Guid pathwayId, Guid patientId, Guid teamId, DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        PathwayId = pathwayId,
        PatientId = patientId,
        TeamId = teamId,
        Reason = PathwayAccessReason.StageOwner,
        StartedAtUtc = nowUtc,
    };

    /// <summary>A consulted team's grant (MC25): read only, ended when the consult closes or is withdrawn.</summary>
    public static PathwayAccess Consult(Guid pathwayId, Guid patientId, Guid teamId, DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        PathwayId = pathwayId,
        PatientId = patientId,
        TeamId = teamId,
        Reason = PathwayAccessReason.Consult,
        StartedAtUtc = nowUtc,
    };
}
