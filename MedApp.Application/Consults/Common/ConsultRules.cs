using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Consults;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Consults.Common;

/// <summary>
/// The consults' rules and words (MC25 of <c>docs/multi-clinic-plan.md</c>; the light demo's <c>consults.ts</c>): only
/// the pathway's own team sends one, to another team in use, once at a time per team asked; only the asked team replies,
/// once; only the asking team closes it (after the reply) or withdraws it (before one). Every change carries the
/// version the caller saw. A restricted or anonymised record can't have a consult sent or replied to; closing and
/// withdrawing stay allowed, as they end the other team's reading.
/// </summary>
public static class ConsultRules
{
    /// <summary>What a refusal across a team wall records as <c>targetKind</c> for a consult.</summary>
    public const string TeamWallTarget = "Consult";

    public const string ChangedMeanwhileMessage =
        "Someone else changed this consult since you opened it. Reload it, then try again.";

    public const string ClosedPathwayMessage = "A completed, cancelled or archived pathway can't have a consult sent.";

    public const string TeamNotInUseMessage = "Choose a team that\u2019s in use.";

    public const string OwnTeamMessage = "Choose another team: this pathway is your own team\u2019s.";

    public const string AlreadyRepliedMessage = "This consult has a reply already, or is closed.";

    public const string NoReplyYetMessage = "It has no reply yet: withdraw it if it\u2019s no longer needed.";

    public const string HasReplyMessage = "It has a reply: close it instead.";

    public const string AlreadyClosedMessage = "This consult is closed already.";

    /// <summary>The pathway statuses that can't have a consult sent.</summary>
    public static readonly IReadOnlySet<PatientPathwayStatus> ClosedPathway = new HashSet<PatientPathwayStatus>
    {
        PatientPathwayStatus.Completed, PatientPathwayStatus.Cancelled, PatientPathwayStatus.Archived
    };

    /// <summary>The 404's words for a consult that doesn't exist, and for one behind the viewer's wall (the same).</summary>
    public static string NotFoundMessage(Guid id) => $"Consult '{id}' was not found.";

    /// <summary>Why a team that doesn't own the pathway can't send a consult about it (a 409).</summary>
    public static string NotPathwaysTeam(string? teamName) =>
        $"This pathway is {Name(teamName)}\u2019s: only its own team asks another team about it.";

    /// <summary>Why a second open consult to the same team is refused (a 409).</summary>
    public static string AlreadyOpen(string? teamName) =>
        $"This pathway already has an open consult with {Name(teamName)}: close or withdraw it first.";

    /// <summary>Why someone outside the asked team can't reply (a 409).</summary>
    public static string NotAskedTeam(string? teamName) =>
        $"This consult was asked of {Name(teamName)}: only that team replies.";

    /// <summary>Why someone outside the asking team can't close or withdraw (a 409).</summary>
    public static string NotAskingTeam(string? teamName) =>
        $"This consult is {Name(teamName)}\u2019s: only the team that asked closes or withdraws it.";

    /// <summary>A 409 unless the caller saw the consult's current version.</summary>
    public static void EnsureVersion(Consult consult, int expectedVersion)
    {
        if (consult.Version != expectedVersion)
            throw new ConflictException(ChangedMeanwhileMessage);
    }

    /// <summary>Why it can't be closed now (only once replied), or null.</summary>
    public static string? WhyCantClose(Consult consult) => consult.Status switch
    {
        ConsultStatus.Replied => null,
        ConsultStatus.Open => NoReplyYetMessage,
        _ => AlreadyClosedMessage,
    };

    /// <summary>Why it can't be withdrawn now (only before a reply), or null.</summary>
    public static string? WhyCantWithdraw(Consult consult) => consult.Status switch
    {
        ConsultStatus.Open => null,
        ConsultStatus.Replied => HasReplyMessage,
        _ => AlreadyClosedMessage,
    };

    private static string Name(string? teamName) => string.IsNullOrWhiteSpace(teamName) ? "another team" : teamName;
}
