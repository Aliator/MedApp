using MedApp.Application.Common.Identity;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.Domain.Patients;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.Common;

public static class ProcedureMapper
{
    /// <summary>Shown in place of a username whose account no longer exists.</summary>
    public const string DeletedUser = "Deleted user";

    /// <summary>A procedure's title and its patient's name only, for a pin's label.</summary>
    public static PatientProcedureLabelResponse ToLabel(ProcedureLabel label) =>
        new(label.Id, label.Title, $"{label.PatientFirstName} {label.PatientLastName}");

    public static string PatientName(Patient? patient) =>
        patient is null ? string.Empty : $"{patient.FirstName} {patient.LastName}";

    /// <summary>
    /// Every user a procedure's full response (or its patient's export) names: assignees, assigners, who started and
    /// finished stages, who wrote its notes, and who deleted any.
    /// </summary>
    public static IEnumerable<Guid> UserIds(PatientProcedure procedure) =>
        procedure.Assignments.SelectMany(a => new[] { a.UserId, a.AssignedByUserId })
            .Concat(procedure.Stages.SelectMany(s => new[] { s.StartedByUserId, s.CompletedByUserId }).OfType<Guid>())
            .Concat(procedure.Notes.SelectMany(n => new[] { n.AuthorUserId, n.DeletedByUserId }).OfType<Guid>())
            .Distinct();

    /// <summary>
    /// A procedure's full response, looking up the usernames it shows. <paramref name="viewerId"/> is who it's for: their
    /// own notes are the ones they can edit.
    /// </summary>
    public static async Task<PatientProcedureResponse> ToResponseAsync(
        PatientProcedure procedure,
        IIdentityReadService identity,
        DateTime nowUtc,
        CancellationToken ct,
        string? patientName = null,
        Guid? viewerId = null,
        bool viewerIsAdmin = false)
    {
        var usernames = await identity.GetUsernamesByIdAsync(UserIds(procedure), ct);
        return ToResponse(procedure, usernames, nowUtc, patientName, viewerId: viewerId, viewerIsAdmin: viewerIsAdmin);
    }

    public static PatientProcedureResponse ToResponse(
        PatientProcedure procedure,
        IReadOnlyDictionary<Guid, string> usernames,
        DateTime nowUtc,
        string? patientName = null,
        Patient? patient = null,
        Guid? viewerId = null,
        bool viewerIsAdmin = false)
    {
        // The patient comes with the procedure when it was loaded with them, or separately for one just created.
        patient ??= procedure.Patient;
        var current = ProcedureWorkflow.CurrentStage(procedure);
        // A restricted or anonymised patient's procedures can't change, so nothing on them is offered.
        var readOnlyReason = PatientRules.WhyCantChange(patient);
        var canChange = readOnlyReason is null;
        // A restricted record's procedures can still be paused: blocked or cancelled, with a reason.
        var canPause = !PatientRules.IsAnonymised(procedure.Patient);

        return new PatientProcedureResponse
        {
            Id = procedure.Id,
            PatientId = procedure.PatientId,
            PatientName = patientName ?? PatientName(patient),
            Patient = ToPatient(patient),
            TemplateId = procedure.TemplateId,
            TemplateName = procedure.TemplateName,
            Title = procedure.Title,
            DueDate = DueDates.ToUkDate(procedure.DueDateUtc),
            DueDateUtc = procedure.DueDateUtc,
            Priority = procedure.Priority.ToString(),
            Status = procedure.Status.ToString(),
            BlockedFromStatus = procedure.BlockedFromStatus?.ToString(),
            IsOverdue = ProcedureWorkflow.IsOverdue(procedure, nowUtc),
            CreatedAt = procedure.CreatedAt,
            LastUpdated = procedure.LastUpdated,
            CurrentStageId = current?.Id,
            CompletedStageCount = procedure.Stages.Count(s => s.IsCompleted),
            StageCount = procedure.Stages.Count,
            AllowedStatusChanges = ProcedureWorkflow.AllowedStatusChanges(procedure)
                .Where(s => canChange || (canPause && PatientRules.PausesWhileRestricted.Contains(s)))
                .Select(s => s.ToString())
                .ToList(),
            CanWorkOnStages = canChange && ProcedureWorkflow.CanWorkOnStages(procedure),
            CanEditDetails = canChange && ProcedureWorkflow.CanEditDetails(procedure),
            CanAddNotes = canChange && ProcedureWorkflow.CanChangeNotes(procedure),
            CanChangeAssignments = canChange && ProcedureWorkflow.CanChangeAssignments(procedure),
            CanBeDeleted = canChange && ProcedureWorkflow.CanBeDeleted(procedure),
            ReadOnlyReason = readOnlyReason,
            PatientFlags = procedure.Patient is null ? null : PatientMapper.ToFlags(procedure.Patient),
            Stages = procedure.Stages
                .OrderBy(s => s.StageOrder)
                .Select(s => new PatientProcedureStageResponse
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
                    State = ProcedureWorkflow.StageState(s),
                    IsCurrent = s.Id == current?.Id,
                    IsLocked = s.IsLocked,
                    CanEdit = canChange && ProcedureWorkflow.CanEditStage(procedure, s),
                    CanUnlock = canChange && ProcedureWorkflow.CanUnlockStage(procedure, s),
                    StartedAtUtc = s.StartedAtUtc,
                    StartedByUserId = s.StartedByUserId,
                    StartedByUsername = Username(usernames, s.StartedByUserId),
                    CompletedAtUtc = s.CompletedAtUtc,
                    CompletedByUserId = s.CompletedByUserId,
                    CompletedByUsername = Username(usernames, s.CompletedByUserId)
                })
                .ToList(),
            Assignments = procedure.Assignments
                .OrderBy(a => a.AssignedAtUtc)
                .Select(a => new PatientProcedureAssignmentResponse
                {
                    UserId = a.UserId,
                    Username = Username(usernames, a.UserId),
                    AssignedByUserId = a.AssignedByUserId,
                    AssignedByUsername = Username(usernames, a.AssignedByUserId),
                    AssignedAtUtc = a.AssignedAtUtc
                })
                .ToList(),
            Notes = ProcedureNoteMapper.FirstNoteText(procedure),
            NoteList = ProcedureNoteMapper.ToResponses(
                procedure.Notes, usernames, viewerId, canChange && ProcedureWorkflow.CanChangeNotes(procedure), canChange, viewerIsAdmin)
        };
    }

    /// <summary>Who a procedure is for, as the patient banner shows them; null when the patient isn't loaded.</summary>
    public static ProcedurePatientResponse? ToPatient(Patient? patient) => patient is null
        ? null
        : new ProcedurePatientResponse
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

    /// <summary>Table rows for procedures, looking up their assignees' usernames.</summary>
    public static async Task<List<PatientProcedureSummaryResponse>> ToSummariesAsync(
        IEnumerable<PatientProcedure> procedures,
        IIdentityReadService identity,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var list = procedures.ToList();
        var usernames = await identity.GetUsernamesByIdAsync(
            list.SelectMany(p => p.Assignments).Select(a => a.UserId).Distinct(), ct);
        return list.Select(p => ToSummary(p, usernames, nowUtc)).ToList();
    }

    public static PatientProcedureSummaryResponse ToSummary(
        PatientProcedure procedure,
        IReadOnlyDictionary<Guid, string> usernames,
        DateTime nowUtc) => new()
    {
        Id = procedure.Id,
        PatientId = procedure.PatientId,
        PatientName = PatientName(procedure.Patient),
        Title = procedure.Title,
        TemplateName = procedure.TemplateName,
        DueDate = DueDates.ToUkDate(procedure.DueDateUtc),
        DueDateUtc = procedure.DueDateUtc,
        Priority = procedure.Priority.ToString(),
        Status = procedure.Status.ToString(),
        IsOverdue = ProcedureWorkflow.IsOverdue(procedure, nowUtc),
        CompletedStageCount = procedure.Stages.Count(s => s.IsCompleted),
        StageCount = procedure.Stages.Count,
        CurrentStageName = ProcedureWorkflow.CurrentStage(procedure)?.Name,
        Assignees = procedure.Assignments
            .Select(a => Username(usernames, a.UserId))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList(),
        LastUpdated = procedure.LastUpdated
    };

    private static string Username(IReadOnlyDictionary<Guid, string> usernames, Guid userId) =>
        usernames.TryGetValue(userId, out var name) ? name : DeletedUser;

    private static string? Username(IReadOnlyDictionary<Guid, string> usernames, Guid? userId) =>
        userId is { } id ? Username(usernames, id) : null;
}
