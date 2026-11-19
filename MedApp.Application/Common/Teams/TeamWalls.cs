namespace MedApp.Application.Common.Teams;

/// <summary>The words of the team walls' refusals, as the light demo's <c>teamWalls.ts</c> says them.</summary>
public static class TeamWalls
{
    /// <summary>What a refusal's <c>targetKind</c> says for a pathway.</summary>
    public const string PathwayTarget = "Pathway";

    /// <summary>What a refusal's <c>targetKind</c> says for a file.</summary>
    public const string AttachmentTarget = "Attachment";

    /// <summary>Why the viewer's team can read a pathway but not change it: it's another team's (a 409).</summary>
    public static string NotYourTeams(string? teamName) =>
        $"This pathway is {teamName ?? "another team"}\u2019s: your team can read it, and works on it only while one of its stages is with your team.";

    /// <summary>Why a team asked a consult (MC25) reads a pathway but can't change it.</summary>
    public static string ConsultReadOnly(string? teamName) =>
        $"Your team was asked a consult by {teamName ?? "another team"}: you can read this pathway until they close it, and reply in Consults.";

    /// <summary>Why an MDT's attendee from another team (MC26) reads a pathway but can't change it.</summary>
    public static string MdtReadOnly(string? teamName) =>
        $"You attend {teamName ?? "another team"}\u2019s MDT, so you can read this pathway (not change it) until it\u2019s discussed or withdrawn and the meeting has ended.";

    /// <summary>Why the viewer's team can't work a stage another team works (MC7, the relay: a 409).</summary>
    public static string StageNotYours(string stageName, string? teamName) =>
        $"The '{stageName}' stage is {teamName ?? "another team"}’s to work: it comes back when they finish it.";

    /// <summary>Why the viewer's team can read a record with no pathway but not change it (a 409).</summary>
    public static string RecordNotYours(string what, string? teamName) =>
        $"{what} is {teamName ?? "another team"}\u2019s: your team can read it, and only its own team changes it.";

    /// <summary>Why a pathway can't be started from another team's template (a 400 on <c>templateId</c>).</summary>
    public static string TemplateNotYours(string templateName) =>
        $"The '{templateName}' template is another team\u2019s. Choose one of your team\u2019s.";
}
