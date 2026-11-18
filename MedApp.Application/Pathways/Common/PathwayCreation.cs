using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Common;

/// <summary>
/// How a patient's pathway is made from a template, and what its creation records: shared by
/// <c>CreatePatientPathwayHandler</c> and medapp-dev's demo data, so both write the same pathway and audit entry.
/// The handler checks first that the patient and template allow it.
/// </summary>
public static class PathwayCreation
{
    /// <summary>
    /// A new, unassigned pathway for a patient, in the template's specialty, with a copy of the template's stages in order (their definitions'
    /// names, descriptions, instructions, SNOMED CT codes and visit requirements), so later edits to the template or
    /// definitions don't change it. The template's maps must have their stage definitions loaded. A blank title
    /// becomes the template's name.
    /// </summary>
    public static PatientPathway FromTemplate(
        PathwayTemplate template,
        Guid patientId,
        string? title,
        DateOnly dueDate,
        PatientPathwayPriority priority,
        DateTime nowUtc)
    {
        var pathwayId = Guid.NewGuid();
        return new PatientPathway
        {
            Id = pathwayId,
            PatientId = patientId,
            TemplateId = template.Id,
            TemplateName = template.Name,
            SpecialtyId = template.SpecialtyId,
            Title = string.IsNullOrWhiteSpace(title) ? template.Name : title.Trim(),
            DueDateUtc = DueDates.EndOfDayUtc(dueDate),
            Priority = priority,
            Status = PatientPathwayStatus.Unassigned,
            CreatedAt = nowUtc,
            LastUpdated = nowUtc,
            Stages = template.Maps
                .OrderBy(m => m.StageOrder)
                .Select((map, index) => new PatientPathwayStage
                {
                    Id = Guid.NewGuid(),
                    PatientPathwayId = pathwayId,
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
    /// Records a new pathway's creation on its change: the Created audit entry (the template's name, the target date,
    /// the priority and how many stages), then its first note, if <paramref name="notes"/> has any text, then the
    /// first stage's visit, if it involves one.
    /// </summary>
    public static void Record(PathwayChange change, string templateName, DateOnly dueDate, string? notes = null)
    {
        var pathway = change.Pathway;
        change.Audit(PatientPathwayAuditAction.Created, new
        {
            template = templateName,
            dueDate,
            priority = pathway.Priority.ToString(),
            stageCount = pathway.Stages.Count
        });

        if (!string.IsNullOrWhiteSpace(notes))
            change.AddNote(notes);

        StageVisits.RequestForCurrentStage(change, []);
    }
}
