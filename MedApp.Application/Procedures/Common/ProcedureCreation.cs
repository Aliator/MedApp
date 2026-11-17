using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.Common;

/// <summary>
/// How a patient's procedure is made from a template, and what its creation records: shared by
/// <c>CreatePatientProcedureHandler</c> and medapp-dev's demo data, so both write the same procedure and audit entry.
/// The handler checks first that the patient and template allow it.
/// </summary>
public static class ProcedureCreation
{
    /// <summary>
    /// A new, unassigned procedure for a patient, with a copy of the template's stages in order (their definitions'
    /// names, descriptions, instructions, SNOMED CT codes and visit requirements), so later edits to the template or
    /// definitions don't change it. The template's maps must have their stage definitions loaded. A blank title
    /// becomes the template's name.
    /// </summary>
    public static PatientProcedure FromTemplate(
        PatientProcedureStageTemplate template,
        Guid patientId,
        string? title,
        string notes,
        DateOnly dueDate,
        PatientProcedurePriority priority,
        DateTime nowUtc)
    {
        var procedureId = Guid.NewGuid();
        return new PatientProcedure
        {
            Id = procedureId,
            PatientId = patientId,
            TemplateId = template.Id,
            TemplateName = template.Name,
            Title = string.IsNullOrWhiteSpace(title) ? template.Name : title.Trim(),
            Notes = notes,
            DueDateUtc = DueDates.EndOfDayUtc(dueDate),
            Priority = priority,
            Status = PatientProcedureStatus.Unassigned,
            CreatedAt = nowUtc,
            LastUpdated = nowUtc,
            Stages = template.Maps
                .OrderBy(m => m.StageOrder)
                .Select((map, index) => new PatientProcedureStage
                {
                    Id = Guid.NewGuid(),
                    PatientProcedureId = procedureId,
                    StageDefinitionId = map.StageDefinitionId,
                    StageOrder = index + 1,
                    Name = map.StageDefinition.Name,
                    Description = map.StageDefinition.Description,
                    Instructions = map.StageDefinition.Instructions,
                    SnomedCode = map.StageDefinition.SnomedCode,
                    SnomedTerm = map.StageDefinition.SnomedTerm,
                    VisitRequirement = map.StageDefinition.VisitRequirement,
                    CreatedAt = nowUtc,
                    LastUpdated = nowUtc
                })
                .ToList()
        };
    }

    /// <summary>
    /// Records a new procedure's creation on its change: the Created audit entry (the template's name, the due date,
    /// the priority and how many stages), then the first stage's visit, if it involves one.
    /// </summary>
    public static void Record(ProcedureChange change, string templateName, DateOnly dueDate)
    {
        var procedure = change.Procedure;
        change.Audit(PatientProcedureAuditAction.Created, new
        {
            template = templateName,
            dueDate,
            priority = procedure.Priority.ToString(),
            stageCount = procedure.Stages.Count
        });

        StageVisits.RequestForCurrentStage(change, []);
    }
}
