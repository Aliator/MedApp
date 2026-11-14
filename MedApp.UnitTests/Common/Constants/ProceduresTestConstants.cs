using MedApp.Application.Procedures.Audit.Queries.SearchProcedureAudit;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.PatientProcedures.Queries.SearchPatientProcedures;
using MedApp.Domain.Patients;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.UnitTests.Common.Constants;

public static class ProceduresTestConstants
{
    /// <summary>"Now" in procedure tests: midday UTC on a summer day, when UK time is BST (UTC+1).</summary>
    public static readonly DateTime Now = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>The UK date at <see cref="Now"/>.</summary>
    public static readonly DateOnly Today = new(2026, 6, 15);

    public static readonly ProcedureActor Staff = new(Guid.Parse("0000000a-0000-0000-0000-000000000001"), "test.staff");
    public static readonly ProcedureActor Admin = new(Guid.Parse("0000000a-0000-0000-0000-000000000002"), "test.admin");
    public static readonly Guid OtherUserId = Guid.Parse("0000000a-0000-0000-0000-000000000003");
    public const string OtherUsername = "other.staff";

    public const string ConsultationStage = "Consultation";
    public const string ProcedureStage = "Procedure";
    public const string TemplateName = "Test template";
    public const string ProcedureTitle = "Test procedure";
    public const string Reason = "Waiting for results.";
    public const int MaxNotesLength = 4000;

    public static readonly Guid ConsultationDefinitionId = Guid.Parse("0000000d-0000-0000-0000-000000000001");
    public static readonly Guid ProcedureDefinitionId = Guid.Parse("0000000d-0000-0000-0000-000000000002");

    /// <summary>The accounts the mocked username lookup knows.</summary>
    public static Dictionary<Guid, string> Usernames => new()
    {
        [Staff.UserId] = Staff.Username,
        [Admin.UserId] = Admin.Username,
        [OtherUserId] = OtherUsername,
    };

    /// <summary>
    /// A procedure with three stages, Consultation, Procedure, Consultation (a repeated stage), due in a week.
    /// Unless Unassigned, <see cref="Staff"/> is assigned. The first <paramref name="finishedStages"/> are finished.
    /// </summary>
    public static PatientProcedure CreateProcedure(
        PatientProcedureStatus status = PatientProcedureStatus.Assigned,
        int finishedStages = 0,
        Patient? patient = null,
        DateTime? dueDateUtc = null,
        PatientProcedurePriority priority = PatientProcedurePriority.Medium,
        string title = ProcedureTitle)
    {
        var id = Guid.NewGuid();
        patient ??= PatientsTestConstants.CreateValidPatient();

        var procedure = new PatientProcedure
        {
            Id = id,
            Title = title,
            Notes = string.Empty,
            DueDateUtc = dueDateUtc ?? Now.AddDays(7),
            Priority = priority,
            Status = status,
            PatientId = patient.Id,
            Patient = patient,
            TemplateId = Guid.NewGuid(),
            TemplateName = TemplateName,
            CreatedAt = Now.AddDays(-7),
            LastUpdated = Now.AddDays(-7),
            Stages =
            [
                CreateStage(id, 1, ConsultationDefinitionId, ConsultationStage),
                CreateStage(id, 2, ProcedureDefinitionId, ProcedureStage),
                CreateStage(id, 3, ConsultationDefinitionId, ConsultationStage),
            ],
        };

        if (status != PatientProcedureStatus.Unassigned)
            procedure.Assignments.Add(new PatientProcedureAssignment
            {
                PatientProcedureId = id,
                UserId = Staff.UserId,
                AssignedByUserId = Admin.UserId,
                AssignedAtUtc = Now.AddDays(-6),
            });

        foreach (var stage in procedure.Stages.Take(finishedStages))
            Finish(stage);

        return procedure;
    }

    public static PatientProcedureStage CreateStage(Guid procedureId, int order, Guid definitionId, string name) => new()
    {
        Id = Guid.NewGuid(),
        PatientProcedureId = procedureId,
        StageOrder = order,
        StageDefinitionId = definitionId,
        Name = name,
        Description = $"{name} description",
        Instructions = $"{name} instructions",
        CreatedAt = Now.AddDays(-7),
        LastUpdated = Now.AddDays(-7),
    };

    /// <summary>Marks a stage started and finished (locked) by <see cref="Staff"/>.</summary>
    public static void Finish(PatientProcedureStage stage)
    {
        stage.StartedAtUtc = Now.AddDays(-5);
        stage.StartedByUserId = Staff.UserId;
        stage.Notes = "Done.";
        stage.IsCompleted = true;
        stage.IsLocked = true;
        stage.CompletedAtUtc = Now.AddDays(-4);
        stage.CompletedByUserId = Staff.UserId;
    }

    /// <summary>The ordered stage at a 1-based position.</summary>
    public static PatientProcedureStage StageAt(this PatientProcedure procedure, int order) =>
        procedure.Stages.Single(s => s.StageOrder == order);

    /// <summary>A search for the first page with no filters (closed procedures hidden), as <see cref="Staff"/>.</summary>
    public static SearchPatientProceduresQuery CreateSearchQuery() =>
        new(1, 10, null, true, null, null, null, null, null, null, false, false, null, null, false, null, Staff.UserId);

    /// <summary>An audit search for the first page with no filters, newest first.</summary>
    public static SearchProcedureAuditQuery CreateAuditSearchQuery() =>
        new(1, 10, false, null, null, null, null, null, null, null);

    public static PatientProcedureStageDefinition CreateDefinition(string name, bool isArchived = false, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = name,
        Description = $"{name} description",
        Instructions = $"{name} instructions",
        IsArchived = isArchived,
    };

    /// <summary>A template listing these definitions in order (repeats allowed), with each map's definition loaded.</summary>
    public static PatientProcedureStageTemplate CreateTemplate(
        string name = TemplateName,
        bool isArchived = false,
        params PatientProcedureStageDefinition[] stages)
    {
        var template = new PatientProcedureStageTemplate { Id = Guid.NewGuid(), Name = name, IsArchived = isArchived };
        template.Maps = stages
            .Select((d, i) => new PatientProcedureStageTemplateMap
            {
                TemplateId = template.Id,
                Template = template,
                StageDefinitionId = d.Id,
                StageDefinition = d,
                StageOrder = i + 1,
            })
            .ToList();
        return template;
    }

    public static PatientProcedureAuditEntry CreateAuditEntry(
        PatientProcedureAuditAction action,
        DateTime atUtc,
        string username = "test.staff",
        string procedureTitle = ProcedureTitle,
        string? stageName = null) => new()
    {
        Id = Guid.NewGuid(),
        PatientProcedureId = Guid.NewGuid(),
        ProcedureTitle = procedureTitle,
        PatientId = Guid.NewGuid(),
        StageId = stageName is null ? null : Guid.NewGuid(),
        StageName = stageName,
        Action = action,
        UserId = Guid.NewGuid(),
        Username = username,
        AtUtc = atUtc,
        Details = "{}",
    };
}
