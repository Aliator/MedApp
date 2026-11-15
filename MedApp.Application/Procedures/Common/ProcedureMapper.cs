using MedApp.Application.Common.Identity;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.Domain.Patients;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.Common;

public static class ProcedureMapper
{
    /// <summary>Shown in place of a username whose account no longer exists.</summary>
    public const string DeletedUser = "Deleted user";

    public static string PatientName(Patient? patient) =>
        patient is null ? string.Empty : $"{patient.FirstName} {patient.LastName}";

    /// <summary>Every user a procedure's full response names: assignees, assigners, and who started and finished stages.</summary>
    public static IEnumerable<Guid> UserIds(PatientProcedure procedure) =>
        procedure.Assignments.SelectMany(a => new[] { a.UserId, a.AssignedByUserId })
            .Concat(procedure.Stages.SelectMany(s => new[] { s.StartedByUserId, s.CompletedByUserId }).OfType<Guid>())
            .Distinct();

    /// <summary>A procedure's full response, looking up the usernames it shows.</summary>
    public static async Task<PatientProcedureResponse> ToResponseAsync(
        PatientProcedure procedure,
        IIdentityReadService identity,
        DateTime nowUtc,
        CancellationToken ct,
        string? patientName = null)
    {
        var usernames = await identity.GetUsernamesByIdAsync(UserIds(procedure), ct);
        return ToResponse(procedure, usernames, nowUtc, patientName);
    }

    public static PatientProcedureResponse ToResponse(
        PatientProcedure procedure,
        IReadOnlyDictionary<Guid, string> usernames,
        DateTime nowUtc,
        string? patientName = null)
    {
        var current = ProcedureWorkflow.CurrentStage(procedure);

        return new PatientProcedureResponse
        {
            Id = procedure.Id,
            PatientId = procedure.PatientId,
            PatientName = patientName ?? PatientName(procedure.Patient),
            TemplateId = procedure.TemplateId,
            TemplateName = procedure.TemplateName,
            Title = procedure.Title,
            Notes = procedure.Notes,
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
            AllowedStatusChanges = ProcedureWorkflow.AllowedStatusChanges(procedure).Select(s => s.ToString()).ToList(),
            CanWorkOnStages = ProcedureWorkflow.CanWorkOnStages(procedure),
            CanEditDetails = ProcedureWorkflow.CanEditDetails(procedure),
            CanChangeAssignments = ProcedureWorkflow.CanChangeAssignments(procedure),
            CanBeDeleted = ProcedureWorkflow.CanBeDeleted(procedure),
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
                    Notes = s.Notes,
                    State = ProcedureWorkflow.StageState(s),
                    IsCurrent = s.Id == current?.Id,
                    IsLocked = s.IsLocked,
                    CanEdit = ProcedureWorkflow.CanEditStage(procedure, s),
                    CanUnlock = ProcedureWorkflow.CanUnlockStage(procedure, s),
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
                .ToList()
        };
    }

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
