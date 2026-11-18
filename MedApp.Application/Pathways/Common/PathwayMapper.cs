using MedApp.Application.Common.Identity;
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
            .Concat(pathway.Stages.SelectMany(s => new[] { s.StartedByUserId, s.CompletedByUserId }).OfType<Guid>())
            .Concat(pathway.Notes.SelectMany(n => new[] { n.AuthorUserId, n.DeletedByUserId }).OfType<Guid>())
            .Distinct();

    /// <summary>
    /// A pathway's full response, looking up the usernames it shows. <paramref name="viewerId"/> is who it's for: their
    /// own notes are the ones they can edit.
    /// </summary>
    public static async Task<PatientPathwayResponse> ToResponseAsync(
        PatientPathway pathway,
        IIdentityReadService identity,
        DateTime nowUtc,
        CancellationToken ct,
        string? patientName = null,
        Guid? viewerId = null,
        bool viewerIsAdmin = false)
    {
        var usernames = await identity.GetUsernamesByIdAsync(UserIds(pathway), ct);
        return ToResponse(pathway, usernames, nowUtc, patientName, viewerId: viewerId, viewerIsAdmin: viewerIsAdmin);
    }

    public static PatientPathwayResponse ToResponse(
        PatientPathway pathway,
        IReadOnlyDictionary<Guid, string> usernames,
        DateTime nowUtc,
        string? patientName = null,
        Patient? patient = null,
        Guid? viewerId = null,
        bool viewerIsAdmin = false)
    {
        // The patient comes with the pathway when it was loaded with them, or separately for one just created.
        patient ??= pathway.Patient;
        var current = PathwayWorkflow.CurrentStage(pathway);
        // A restricted or anonymised patient's pathways can't change, so nothing on them is offered.
        var readOnlyReason = PatientRules.WhyCantChange(patient);
        var canChange = readOnlyReason is null;
        // A restricted record's pathways can still be paused: blocked or cancelled, with a reason.
        var canPause = !PatientRules.IsAnonymised(pathway.Patient);

        return new PatientPathwayResponse
        {
            Id = pathway.Id,
            PatientId = pathway.PatientId,
            PatientName = patientName ?? PatientName(patient),
            Patient = ToPatient(patient),
            TemplateId = pathway.TemplateId,
            TemplateName = pathway.TemplateName,
            Title = pathway.Title,
            DueDate = DueDates.ToUkDate(pathway.DueDateUtc),
            DueDateUtc = pathway.DueDateUtc,
            Priority = pathway.Priority.ToString(),
            Status = pathway.Status.ToString(),
            BlockedFromStatus = pathway.BlockedFromStatus?.ToString(),
            IsOverdue = PathwayWorkflow.IsOverdue(pathway, nowUtc),
            CreatedAt = pathway.CreatedAt,
            LastUpdated = pathway.LastUpdated,
            CurrentStageId = current?.Id,
            CompletedStageCount = pathway.Stages.Count(s => s.IsCompleted),
            StageCount = pathway.Stages.Count,
            AllowedStatusChanges = PathwayWorkflow.AllowedStatusChanges(pathway)
                .Where(s => canChange || (canPause && PatientRules.PausesWhileRestricted.Contains(s)))
                .Select(s => s.ToString())
                .ToList(),
            CanWorkOnStages = canChange && PathwayWorkflow.CanWorkOnStages(pathway),
            CanEditDetails = canChange && PathwayWorkflow.CanEditDetails(pathway),
            CanAddNotes = canChange && PathwayWorkflow.CanChangeNotes(pathway),
            CanChangeAssignments = canChange && PathwayWorkflow.CanChangeAssignments(pathway),
            CanBeDeleted = canChange && PathwayWorkflow.CanBeDeleted(pathway),
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
                    Notes = s.Notes,
                    State = PathwayWorkflow.StageState(s),
                    IsCurrent = s.Id == current?.Id,
                    IsLocked = s.IsLocked,
                    CanEdit = canChange && PathwayWorkflow.CanEditStage(pathway, s),
                    CanUnlock = canChange && PathwayWorkflow.CanUnlockStage(pathway, s),
                    StartedAtUtc = s.StartedAtUtc,
                    StartedByUserId = s.StartedByUserId,
                    StartedByUsername = Username(usernames, s.StartedByUserId),
                    CompletedAtUtc = s.CompletedAtUtc,
                    CompletedByUserId = s.CompletedByUserId,
                    CompletedByUsername = Username(usernames, s.CompletedByUserId)
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
                pathway.Notes, usernames, viewerId, canChange && PathwayWorkflow.CanChangeNotes(pathway), canChange, viewerIsAdmin)
        };
    }

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
        StageCount = pathway.Stages.Count,
        CurrentStageName = PathwayWorkflow.CurrentStage(pathway)?.Name,
        Assignees = pathway.Assignments
            .Select(a => Username(usernames, a.UserId))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList(),
        LastUpdated = pathway.LastUpdated
    };

    private static string Username(IReadOnlyDictionary<Guid, string> usernames, Guid userId) =>
        usernames.TryGetValue(userId, out var name) ? name : DeletedUser;

    private static string? Username(IReadOnlyDictionary<Guid, string> usernames, Guid? userId) =>
        userId is { } id ? Username(usernames, id) : null;
}
