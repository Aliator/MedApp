namespace MedApp.Contracts.Patients.Responses;

/// <summary>
/// A patient's pathway in a team the viewer can't see into (decision 183 of <c>docs/multi-clinic-plan.md</c>): only that
/// there is one, in which clinic, and since when. No title, stages, notes, files, ID or link.
/// </summary>
/// <param name="TeamName">The clinic (or the lab) it's in.</param>
/// <param name="State"><c>InProgress</c> or <c>Completed</c>.</param>
/// <param name="Since">When it started (in progress) or was completed, as a UK date.</param>
public sealed record OtherTeamPathwaySummaryResponse(
    string TeamName,
    string State,
    DateOnly Since);
