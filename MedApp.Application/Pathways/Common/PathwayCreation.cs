using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Common;

/// <summary>
/// How a patient's pathway is made from a template, and what its creation records: shared by
/// <c>CreatePatientPathwayHandler</c>, accepting a referral and medapp-dev's demo data, so all write the same pathway and
/// audit entry. The handler checks first that the patient and template allow it.
/// </summary>
public static class PathwayCreation
{
    /// <summary>
    /// A new, unassigned pathway for a patient, in the template's team, keeping the template's whole tree as its plan
    /// (<see cref="PatientPathwayPlan"/>: each stage's definition's names, descriptions, instructions, SNOMED CT codes, visit
    /// requirements and gates, the template's options, owners, choice nodes and links, and its addable branches), so
    /// later edits to the template or definitions don't change it. It starts with the stages up to its first choice
    /// node (the owner's model, MC10b: <see cref="PathwayGrowth"/>). The template's maps must have their stage
    /// definitions loaded; <paramref name="branchDefinitions"/> are its addable branches' (a branch with a definition
    /// missing is left out). A blank title becomes the template's name.
    /// </summary>
    public static PatientPathway FromTemplate(
        PathwayTemplate template,
        Guid patientId,
        string? title,
        DateOnly dueDate,
        PatientPathwayPriority priority,
        DateTime nowUtc,
        IReadOnlyDictionary<Guid, PatientPathwayStageDefinition>? branchDefinitions = null)
    {
        var pathway = new PatientPathway
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            TemplateId = template.Id,
            TemplateName = template.Name,
            TeamId = template.TeamId,
            RetentionClass = template.RetentionClass,
            Title = string.IsNullOrWhiteSpace(title) ? template.Name : title.Trim(),
            DueDateUtc = DueDates.EndOfDayUtc(dueDate),
            Priority = priority,
            Status = PatientPathwayStatus.Unassigned,
            CreatedAt = nowUtc,
            LastUpdated = nowUtc,
            Plan = PlanOf(template, branchDefinitions)
        };

        PathwayGrowth.Grow(pathway, nowUtc);
        return pathway;
    }

    /// <summary>A template's whole tree, as a pathway keeps it (<see cref="PatientPathwayPlan"/>).</summary>
    public static PatientPathwayPlan PlanOf(
        PathwayTemplate template, IReadOnlyDictionary<Guid, PatientPathwayStageDefinition>? branchDefinitions = null)
    {
        var maps = template.Maps.OrderBy(m => m.StageOrder).ToList();
        var stages = maps
            .Select((map, index) => Planned(
                map.StageDefinition, index + 1, map.IsOptional, map.StopsClock, map.ChoiceKind, map.Outcomes,
                map.OwnerTeamId == template.TeamId ? null : map.OwnerTeamId))
            .ToList();

        // Each map's place is its index + 1; a template's links are by those places (a chain when it has none).
        var links = template.Links.Count > 0 ? template.Links.ToList() : PathwayGraph.ChainLinks(stages.Count);

        var branches = template.AddableBranches
            .Where(b => b.StageDefinitionIds.Count > 0
                        && b.StageDefinitionIds.All(id => branchDefinitions?.ContainsKey(id) == true))
            .Select(b => new PathwayPlanBranch(
                b.Id,
                b.Name,
                b.StageDefinitionIds.Select(id => Planned(branchDefinitions![id], 0, false, false, null, [], null)).ToList(),
                b.AttachAfterStageOrders.ToList(),
                b.IsRepeatable))
            .ToList();

        return new PatientPathwayPlan(stages, links, branches);
    }

    private static PathwayPlanStage Planned(
        PatientPathwayStageDefinition definition,
        int order,
        bool isOptional,
        bool stopsClock,
        StageChoiceKind? choiceKind,
        IReadOnlyList<PathwayTemplateOutcome> outcomes,
        Guid? ownerTeamId) =>
        new(
            order,
            definition.Id,
            definition.Name,
            definition.Description,
            definition.Instructions,
            definition.SnomedCode,
            definition.SnomedTerm,
            definition.VisitRequirement,
            definition.NeedsLetterKind,
            definition.WaitsForResults,
            definition.NeedsConsent,
            isOptional,
            stopsClock,
            choiceKind,
            choiceKind is null ? [] : outcomes.ToList(),
            ownerTeamId);

    /// <summary>
    /// Records a new pathway's creation on its change: its own team's access grant (<c>Owner</c>, the team walls), the
    /// Created audit entry (the template's name, the target date, the priority and how many stages it starts with, and
    /// the referral it was accepted from, if <paramref name="referralId"/> is given), then its first note, if
    /// <paramref name="notes"/> has any text, then its first stage reached (handed over at once if another team works
    /// it: MC7) and its visit asked for, if it involves one.
    /// </summary>
    public static void Record(PathwayChange change, string templateName, DateOnly dueDate, string? notes = null, Guid? referralId = null)
    {
        var pathway = change.Pathway;
        change.GrantOwner();
        change.Audit(PatientPathwayAuditAction.Created, referralId is { } fromReferral
            ? new
            {
                template = templateName,
                dueDate,
                priority = pathway.Priority.ToString(),
                stageCount = pathway.Stages.Count,
                referralId = fromReferral
            }
            : new
            {
                template = templateName,
                dueDate,
                priority = pathway.Priority.ToString(),
                stageCount = pathway.Stages.Count
            });

        if (!string.IsNullOrWhiteSpace(notes))
            change.AddNote(notes);

        change.ReachNext(pathway.TeamId);
        StageVisits.RequestForCurrentStage(change, []);
    }
}
