using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Domain.Patients;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Common;

public static class PathwayMapper
{
    /// <summary>Shown in place of a username whose account no longer exists.</summary>
    public const string DeletedUser = "Deleted user";

    /// <summary>A pathway's title and its patient's name only, for a pin's label.</summary>
    public static PatientPathwayLabelResponse ToLabel(PathwayLabel label) =>
        new(label.Id, label.Title, $"{label.PatientFirstName} {label.PatientLastName}");

    public static string PatientName(Patient? patient) =>
        patient is null ? string.Empty : $"{patient.FirstName} {patient.LastName}";

    /// <summary>
    /// Every user a pathway's full response (or its patient's export) names: assignees, assigners, who started and
    /// finished stages, who wrote its notes, and who deleted any.
    /// </summary>
    public static IEnumerable<Guid> UserIds(PatientPathway pathway) =>
        pathway.Assignments.SelectMany(a => new[] { a.UserId, a.AssignedByUserId })
            .Concat(pathway.Stages.SelectMany(s => new[]
            {
                s.StartedByUserId, s.CompletedByUserId, s.SkippedByUserId, s.PickedUpByUserId, s.ResultEnteredByUserId
            }).OfType<Guid>())
            .Concat(pathway.BranchAdditions.Select(b => b.AddedByUserId))
            .Concat(pathway.Notes.SelectMany(n => new[] { n.AuthorUserId, n.DeletedByUserId }).OfType<Guid>())
            .Distinct();

    /// <summary>
    /// A pathway's full response, looking up the usernames it shows. <paramref name="viewerId"/> is who it's for: their
    /// own notes are the ones they can edit. <paramref name="viewerPermissions"/> is what their roles allow: see
    /// <see cref="ToResponse"/>.
    /// </summary>
    public static async Task<PatientPathwayResponse> ToResponseAsync(
        PatientPathway pathway,
        IIdentityReadService identity,
        DateTime nowUtc,
        CancellationToken ct,
        string? patientName = null,
        Guid? viewerId = null,
        IReadOnlySet<Permission>? viewerPermissions = null,
        TeamScope? viewerScope = null,
        string? teamName = null,
        IReadOnlyDictionary<Guid, string>? teamNames = null)
    {
        var usernames = await identity.GetUsernamesByIdAsync(UserIds(pathway), ct);
        return ToResponse(
            pathway, usernames, nowUtc, patientName, viewerId: viewerId, viewerPermissions: viewerPermissions,
            viewerScope: viewerScope, teamName: teamName, teamNames: teamNames);
    }

    /// <summary>
    /// A pathway's full response. Its <c>Can...</c> flags say what this viewer can do: what the pathway and its
    /// patient's record allow, and what <paramref name="viewerPermissions"/> (the viewer's, from their session) allow, as
    /// the endpoints check. Working on it (stages, details, notes, status changes) needs
    /// <see cref="Permission.WorkPathways"/>, assignments <see cref="Permission.AssignPathways"/>, unlocking or reinstating a stage
    /// <see cref="Permission.UnlockStages"/>, skipping one whose visit is Required <see cref="Permission.FinishWithoutVisit"/>, deleting it <see cref="Permission.DeletePathways"/>, and deleting a note
    /// <see cref="Permission.CorrectRecords"/>.
    /// <para>
    /// Null <paramref name="viewerPermissions"/> means no viewer is known: the flags then say only what the pathway and
    /// record allow, with nothing that needs <see cref="Permission.CorrectRecords"/>. Every response to a request passes
    /// the caller's.
    /// </para>
    /// <para>
    /// <paramref name="viewerScope"/> is the viewer's team walls: only the pathway's own team changes it, so for anyone
    /// else (an admin, the lab after hand-back) every flag is off and <c>ReadOnlyReason</c> says whose it is
    /// (<paramref name="teamName"/>), except to an admin, who sees every team's records read only. Null: the viewer is
    /// the change's own actor, whose team owns it.
    /// </para>
    /// <para>
    /// The relay (MC7): the team that works a current stage (the lab, for its stages) works that stage, picks it up and
    /// adds notes too, though the pathway stays its own team's; the pathway's own team reads a stage another team has
    /// (<c>IsWithAnotherTeam</c>). Teams are named from <paramref name="teamNames"/> or those loaded with the pathway.
    /// </para>
    /// </summary>
    public static PatientPathwayResponse ToResponse(
        PatientPathway pathway,
        IReadOnlyDictionary<Guid, string> usernames,
        DateTime nowUtc,
        string? patientName = null,
        Patient? patient = null,
        Guid? viewerId = null,
        IReadOnlySet<Permission>? viewerPermissions = null,
        TeamScope? viewerScope = null,
        string? teamName = null,
        IReadOnlyDictionary<Guid, string>? teamNames = null)
    {
        // The patient comes with the pathway when it was loaded with them, or separately for one just created.
        patient ??= pathway.Patient;
        var currents = PathwayWorkflow.CurrentStages(pathway);
        var currentIds = currents.Select(s => s.Id).ToHashSet();
        // A restricted or anonymised patient's pathways can't change, so nothing on them is offered.
        var readOnlyReason = PatientRules.WhyCantChange(patient);
        var canChange = readOnlyReason is null;
        // Only the pathway's own team changes it (the team walls): admins and the lab read it.
        var mine = viewerScope is null || TeamAccess.CanChange(pathway, viewerScope);
        // The relay: the team working a current stage (the lab, for its stages) works that stage and adds notes too.
        var worksCurrent = viewerScope is not null && PathwayRepositoryExtensions.WorksCurrentStage(pathway, viewerScope);
        bool WorksStage(PatientPathwayStage s) => viewerScope is null || TeamAccess.CanWorkStage(pathway, s, viewerScope);
        string? NameOf(Guid? id) => TeamName(pathway, id, teamNames);
        if (readOnlyReason is null && !mine && !worksCurrent && viewerScope is { SeesAllTeams: false })
            readOnlyReason = TeamWalls.NotYourTeams(teamName ?? NameOf(pathway.TeamId));
        // A restricted record's pathways can still be paused: blocked or cancelled, with a reason.
        var canPause = !PatientRules.IsAnonymised(pathway.Patient);
        var canWork = Allows(viewerPermissions, Permission.WorkPathways) && mine;
        var worksPathways = Allows(viewerPermissions, Permission.WorkPathways) && (mine || worksCurrent);
        var workable = worksPathways && canChange && PathwayWorkflow.CanWorkOnStages(pathway);
        var addable = AddableBranches(pathway, canWork && canChange);

        return new PatientPathwayResponse
        {
            Id = pathway.Id,
            PatientId = pathway.PatientId,
            PatientName = patientName ?? PatientName(patient),
            Patient = ToPatient(patient),
            TemplateId = pathway.TemplateId,
            TemplateName = pathway.TemplateName,
            TeamId = pathway.TeamId,
            Title = pathway.Title,
            DueDate = DueDates.ToUkDate(pathway.DueDateUtc),
            DueDateUtc = pathway.DueDateUtc,
            Priority = pathway.Priority.ToString(),
            Status = pathway.Status.ToString(),
            BlockedFromStatus = pathway.BlockedFromStatus?.ToString(),
            IsOverdue = PathwayWorkflow.IsOverdue(pathway, nowUtc),
            CreatedAt = pathway.CreatedAt,
            LastUpdated = pathway.LastUpdated,
            CurrentStageId = currents.FirstOrDefault()?.Id,
            CurrentStageIds = currents.Select(s => s.Id).ToList(),
            CompletedStageCount = pathway.Stages.Count(s => s.IsCompleted),
            SkippedStageCount = pathway.Stages.Count(s => s.IsSkipped),
            StageCount = pathway.Stages.Count,
            AllowedStatusChanges = canWork
                ? PathwayWorkflow.AllowedStatusChanges(pathway)
                    .Where(s => canChange || (canPause && PatientRules.PausesWhileRestricted.Contains(s)))
                    .Select(s => s.ToString())
                    .ToList()
                : [],
            CanWorkOnStages = workable,
            CanEditDetails = canWork && canChange && PathwayWorkflow.CanEditDetails(pathway),
            CanAddNotes = worksPathways && canChange && PathwayWorkflow.CanChangeNotes(pathway),
            CanChangeAssignments = Allows(viewerPermissions, Permission.AssignPathways)
                && mine && canChange && PathwayWorkflow.CanChangeAssignments(pathway),
            CanBeDeleted = Allows(viewerPermissions, Permission.DeletePathways)
                && mine && canChange && PathwayWorkflow.CanBeDeleted(pathway),
            ReadOnlyReason = readOnlyReason,
            PatientFlags = pathway.Patient is null ? null : PatientMapper.ToFlags(pathway.Patient),
            Stages = pathway.Stages
                .OrderBy(s => s.StageOrder)
                .Select(s => new PatientPathwayStageResponse
                {
                    Id = s.Id,
                    StageDefinitionId = s.StageDefinitionId,
                    StageOrder = s.StageOrder,
                    Name = s.Name,
                    Description = s.Description,
                    Instructions = s.Instructions,
                    SnomedCode = s.SnomedCode,
                    SnomedTerm = s.SnomedTerm,
                    VisitRequirement = s.VisitRequirement.ToString(),
                    NeedsLetterKind = s.NeedsLetterKind?.ToString(),
                    WaitsForResults = s.WaitsForResults,
                    NeedsConsent = s.NeedsConsent,
                    IsOptional = s.IsOptional,
                    ChoiceKind = s.ChoiceKind?.ToString(),
                    Outcomes = PathwayGrowth.Outcomes(pathway, s)
                        .Select(o => new PatientPathwayOutcomeResponse { Name = o.Name, StageNames = o.StageNames.ToList() })
                        .ToList(),
                    Outcome = s.Outcome,
                    NeedsOutcome = s.ChoiceKind is not null && s.Outcome is null,
                    // The lab's results: shown on the pathway (as a note is), never in the audit log.
                    Result = s.ResultText is null
                        ? null
                        : new PatientPathwayStageResultResponse
                        {
                            Text = s.ResultText,
                            EnteredByUsername = Username(usernames, s.ResultEnteredByUserId),
                            EnteredAtUtc = s.ResultEnteredAtUtc
                        },
                    // A lab stage's case (MC16), for its page to link to: its open one, else its latest.
                    LabCaseId = LabCaseOf(s)?.Id,
                    LabCaseNumber = LabCaseOf(s)?.Number,
                    LabCaseStatus = LabCaseOf(s)?.Status.ToString(),
                    PredecessorIds = PathwayGraph.Predecessors(pathway, s).Select(x => x.Id).ToList(),
                    BranchAdditionId = s.BranchAdditionId,
                    Notes = s.Notes,
                    State = PathwayWorkflow.StageState(s),
                    IsCurrent = currentIds.Contains(s.Id),
                    IsLocked = s.IsLocked,
                    CanEdit = workable && WorksStage(s) && PathwayWorkflow.CanEditStage(pathway, s),
                    CanUnlock = Allows(viewerPermissions, Permission.UnlockStages)
                        && mine && canChange && PathwayWorkflow.CanUnlockStage(pathway, s),
                    CanSkip = worksPathways && WorksStage(s) && canChange
                        && PathwayWorkflow.CanSkipStage(pathway, s, Allows(viewerPermissions, Permission.FinishWithoutVisit)),
                    CanReinstate = Allows(viewerPermissions, Permission.UnlockStages)
                        && mine && canChange && PathwayWorkflow.CanReinstateStage(pathway, s),
                    SkippedAtUtc = s.SkippedAtUtc,
                    SkippedByUserId = s.SkippedByUserId,
                    SkippedByUsername = Username(usernames, s.SkippedByUserId),
                    SkipReason = s.SkipReason?.ToString(),
                    SkipDetail = s.SkipDetail,
                    CanRecordSpecimen = Allows(viewerPermissions, Permission.RecordSpecimens) && mine
                        && Specimens.Common.SpecimenWorkflow.WhyCantTake(pathway, s) is null,
                    CanRecordConsent = Allows(viewerPermissions, Permission.RecordConsent) && mine
                        && Consent.Common.ConsentWorkflow.WhyCantRecord(pathway, s) is null,
                    StartedAtUtc = s.StartedAtUtc,
                    StartedByUserId = s.StartedByUserId,
                    StartedByUsername = Username(usernames, s.StartedByUserId),
                    CompletedAtUtc = s.CompletedAtUtc,
                    CompletedByUserId = s.CompletedByUserId,
                    CompletedByUsername = Username(usernames, s.CompletedByUserId),
                    OwnerTeamId = s.OwnerTeamId,
                    OwnerTeamName = NameOf(s.OwnerTeamId),
                    IsWithAnotherTeam = currentIds.Contains(s.Id)
                        && PathwayGraph.StageTeam(pathway, s) != pathway.TeamId && !WorksStage(s),
                    ReachedAtUtc = s.ReachedAtUtc,
                    HandedOverFromTeamName = NameOf(s.HandedOverFromTeamId),
                    PickedUpAtUtc = s.PickedUpAtUtc,
                    PickedUpByUserId = s.PickedUpByUserId,
                    PickedUpByUsername = Username(usernames, s.PickedUpByUserId),
                    CanPickUp = workable && currentIds.Contains(s.Id) && WorksStage(s) && AwaitsPickUp(s)
                })
                .ToList(),
            Assignments = pathway.Assignments
                .OrderBy(a => a.AssignedAtUtc)
                .Select(a => new PatientPathwayAssignmentResponse
                {
                    UserId = a.UserId,
                    Username = Username(usernames, a.UserId),
                    AssignedByUserId = a.AssignedByUserId,
                    AssignedByUsername = Username(usernames, a.AssignedByUserId),
                    AssignedAtUtc = a.AssignedAtUtc
                })
                .ToList(),
            Notes = PathwayNoteMapper.FirstNoteText(pathway),
            NoteList = PathwayNoteMapper.ToResponses(
                pathway.Notes, usernames, viewerId, canChange && PathwayWorkflow.CanChangeNotes(pathway), canChange, viewerPermissions,
                viewerTeamOwns: mine || worksCurrent),
            CanAddBranch = addable.Any(b => b.AfterStageIds.Count > 0),
            AddableBranches = addable,
            // The reason is the clinician's own words: on the pathway (as a note is), never in the audit log.
            BranchAdditions = pathway.BranchAdditions
                .OrderBy(b => b.AddedAtUtc).ThenBy(b => b.Id)
                .Select(b => new PatientPathwayBranchAdditionResponse
                {
                    Id = b.Id,
                    TemplateBranchId = b.TemplateBranchId,
                    Name = b.Name,
                    AfterStageId = b.AfterStageId,
                    StageIds = b.StageIds.ToList(),
                    Reason = b.Reason,
                    AddedByUsername = Username(usernames, b.AddedByUserId),
                    AddedAtUtc = b.AddedAtUtc
                })
                .ToList()
        };
    }

    /// <summary>A stage handed over and waiting for its team to pick it up (MC7).</summary>
    /// <summary>A lab stage's case (MC16): its open one, else its latest not entered in error; none for a clinic's stage.</summary>
    public static Domain.Lab.LabCase? LabCaseOf(PatientPathwayStage stage) =>
        stage.OwnerTeam?.Kind == Domain.Teams.TeamKind.Lab ? Lab.Common.LabStages.CaseOf(stage) : null;

    public static bool AwaitsPickUp(PatientPathwayStage stage) =>
        stage.HandedOverFromTeamId is not null && stage.PickedUpAtUtc is null;

    /// <summary>
    /// The pathway's addable branches (MC10) as its page lists them: in the template's order, never suggested or
    /// pre-selected, each with the stages it can be added after now (none when <paramref name="canAdd"/> is false).
    /// </summary>
    public static List<PatientPathwayAddableBranchResponse> AddableBranches(PatientPathway pathway, bool canAdd) =>
        (pathway.Plan?.Branches ?? [])
            .Select(b => new PatientPathwayAddableBranchResponse
            {
                TemplateBranchId = b.Id,
                Name = b.Name,
                StageNames = b.Stages.Select(s => s.Name).ToList(),
                AfterStageIds = canAdd ? PathwayBranches.AfterStagesFor(pathway, b).Select(s => s.Id).ToList() : [],
                IsRepeatable = b.IsRepeatable,
                TimesAdded = pathway.BranchAdditions.Count(a => a.TemplateBranchId == b.Id)
            })
            .ToList();

    /// <summary>A team's name: from <paramref name="teamNames"/>, or the teams loaded with the pathway; null for none.</summary>
    public static string? TeamName(PatientPathway pathway, Guid? teamId, IReadOnlyDictionary<Guid, string>? teamNames = null)
    {
        if (teamId is not { } id)
            return null;

        if (teamNames?.GetValueOrDefault(id) is { } named)
            return named;

        if (pathway.Team?.Id == id)
            return pathway.Team.Name;

        return pathway.Stages
            .SelectMany(s => new[] { s.OwnerTeam, s.HandedOverFromTeam })
            .FirstOrDefault(t => t?.Id == id)?.Name;
    }

    /// <summary>"A", "A and B", "A, B and C".</summary>
    public static string AndList(IReadOnlyList<string> words) =>
        words.Count < 2 ? words.FirstOrDefault() ?? string.Empty : $"{string.Join(", ", words.Take(words.Count - 1))} and {words[^1]}";

    /// <summary>Who a pathway is for, as the patient banner shows them; null when the patient isn't loaded.</summary>
    public static PathwayPatientResponse? ToPatient(Patient? patient) => patient is null
        ? null
        : new PathwayPatientResponse
        {
            Id = patient.Id,
            FirstName = patient.FirstName,
            LastName = patient.LastName,
            DateOfBirth = patient.DateOfBirth,
            NhsNumber = patient.NhsNumber,
            IsRestricted = patient.RestrictedAtUtc is not null,
            IsDisputed = patient.DisputedAtUtc is not null,
            IsAnonymised = patient.AnonymisedAtUtc is not null
        };

    /// <summary>Table rows for pathways, looking up their assignees' usernames.</summary>
    public static async Task<List<PatientPathwaySummaryResponse>> ToSummariesAsync(
        IEnumerable<PatientPathway> pathways,
        IIdentityReadService identity,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var list = pathways.ToList();
        var usernames = await identity.GetUsernamesByIdAsync(
            list.SelectMany(p => p.Assignments).Select(a => a.UserId).Distinct(), ct);
        return list.Select(p => ToSummary(p, usernames, nowUtc)).ToList();
    }

    public static PatientPathwaySummaryResponse ToSummary(
        PatientPathway pathway,
        IReadOnlyDictionary<Guid, string> usernames,
        DateTime nowUtc) => new()
    {
        Id = pathway.Id,
        PatientId = pathway.PatientId,
        PatientName = PatientName(pathway.Patient),
        Title = pathway.Title,
        TemplateName = pathway.TemplateName,
        DueDate = DueDates.ToUkDate(pathway.DueDateUtc),
        DueDateUtc = pathway.DueDateUtc,
        Priority = pathway.Priority.ToString(),
        Status = pathway.Status.ToString(),
        IsOverdue = PathwayWorkflow.IsOverdue(pathway, nowUtc),
        CompletedStageCount = pathway.Stages.Count(s => s.IsCompleted),
        SkippedStageCount = pathway.Stages.Count(s => s.IsSkipped),
        StageCount = pathway.Stages.Count,
        // Every current stage, so a route side by side isn't missed in a list (H-69), and who has them (the relay).
        CurrentStageName = CurrentStageNames(pathway),
        CurrentStageTeamName = CurrentStageTeamNames(pathway),
        AwaitingPickUp = PathwayWorkflow.CurrentStages(pathway).Any(AwaitsPickUp),
        Assignees = pathway.Assignments
            .Select(a => Username(usernames, a.UserId))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList(),
        LastUpdated = pathway.LastUpdated,
        Clock = pathway.Referral is { } referral
            ? Referrals.Common.ReferralMapper.ToClock(referral, DueDates.ToUkDate(nowUtc))
            : null
    };

    private static string? CurrentStageNames(PatientPathway pathway) =>
        AndList(PathwayWorkflow.CurrentStages(pathway).Select(s => s.Name).ToList()) is { Length: > 0 } names ? names : null;

    private static string? CurrentStageTeamNames(PatientPathway pathway) =>
        AndList(PathwayWorkflow.CurrentStages(pathway)
                .Select(s => TeamName(pathway, PathwayGraph.StageTeam(pathway, s)))
                .OfType<string>()
                .Distinct()
                .ToList()) is { Length: > 0 } names
            ? names
            : null;

    /// <summary>
    /// Whether the viewer's permissions allow a flag that needs this one; with no viewer known (null), the flag is left to
    /// what the pathway and record allow. Not for <see cref="Permission.CorrectRecords"/>, which needs a known holder.
    /// </summary>
    internal static bool Allows(IReadOnlySet<Permission>? viewerPermissions, Permission permission) =>
        viewerPermissions is null || viewerPermissions.Contains(permission);

    private static string Username(IReadOnlyDictionary<Guid, string> usernames, Guid userId) =>
        usernames.TryGetValue(userId, out var name) ? name : DeletedUser;

    private static string? Username(IReadOnlyDictionary<Guid, string> usernames, Guid? userId) =>
        userId is { } id ? Username(usernames, id) : null;
}
