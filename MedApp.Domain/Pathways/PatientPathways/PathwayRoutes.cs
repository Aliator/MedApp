namespace MedApp.Domain.Pathways.PatientPathways;

/// <summary>
/// A link between two of a template's stages, by their 1-based orders (MC8, tree pathways): the stage at
/// <see cref="ToStageOrder"/> comes after the one at <see cref="FromStageOrder"/>. A stage with several successors is a
/// fork (or a choice node's outcomes); one with several predecessors is a join.
/// </summary>
public sealed record PathwayTemplateLink(int FromStageOrder, int ToStageOrder);

/// <summary>
/// One outcome of a choice node (MC10b): its admin-written name, and the stages straight after the choice node it leads
/// to (by order; none ends the route there).
/// </summary>
public sealed record PathwayTemplateOutcome(string Name, IReadOnlyList<int> NextStageOrders);

/// <summary>
/// A branch a template lets staff add to a pathway part-way, on new information (MC10): its stages (a chain, from the
/// stage library) and the template stages it may follow (none: any). Not created with the pathway; added with a reason.
/// </summary>
public sealed record PathwayTemplateBranch(
    Guid Id,
    string Name,
    IReadOnlyList<Guid> StageDefinitionIds,
    IReadOnlyList<int> AttachAfterStageOrders,
    bool IsRepeatable);

/// <summary>
/// The template's whole tree as a pathway keeps it when it's made (the owner's model, MC10b): every stage with its
/// copied details and options, the links, and the addable branches, so later edits to the template don't change it. A
/// pathway holds only the stages it has grown to (up to its next choice node not yet recorded); the rest stay here.
/// </summary>
public sealed record PathwayPlan(
    IReadOnlyList<PathwayPlanStage> Stages,
    IReadOnlyList<PathwayTemplateLink> Links,
    IReadOnlyList<PathwayPlanBranch> Branches);

/// <summary>A planned stage: what a pathway's stage is copied from when it's reached (the template's options, the definition's details).</summary>
public sealed record PathwayPlanStage(
    int TemplateStageOrder,
    Guid StageDefinitionId,
    string Name,
    string Description,
    string Instructions,
    string? SnomedCode,
    string? SnomedTerm,
    VisitRequirement VisitRequirement,
    Letters.LetterKind? NeedsLetterKind,
    bool WaitsForResults,
    bool NeedsConsent,
    bool IsOptional,
    bool StopsClock,
    StageChoiceKind? ChoiceKind,
    IReadOnlyList<PathwayTemplateOutcome> Outcomes,
    Guid? OwnerTeamId);

/// <summary>A template's addable branch as a pathway keeps it: its stages (a chain) copied from their definitions.</summary>
public sealed record PathwayPlanBranch(
    Guid Id,
    string Name,
    IReadOnlyList<PathwayPlanStage> Stages,
    IReadOnlyList<int> AttachAfterStageOrders,
    bool IsRepeatable);

/// <summary>
/// A link between two of a pathway's stages (MC8): <see cref="ToStageId"/> comes after <see cref="FromStageId"/>. A
/// stage is current when it's still to do and nothing before it, on any route, is. No foreign keys to the stages (the
/// pathway's cascade removes them).
/// </summary>
public sealed class PatientPathwayStageLink
{
    public Guid PatientPathwayId { get; init; }
    public Guid FromStageId { get; init; }
    public Guid ToStageId { get; init; }
}

/// <summary>
/// A template's addable branch added to a pathway (MC10), with the clinician's reason: free text, shown on the pathway
/// as a note is, never in the audit log, replaced when the record is anonymised. No foreign key to the pathway beyond
/// its cascade; the patient is copied for the export and anonymisation.
/// </summary>
public sealed class PathwayBranchAddition
{
    public Guid Id { get; init; }
    public Guid PatientPathwayId { get; init; }
    public Guid PatientId { get; init; }
    public Guid TemplateBranchId { get; init; }

    /// <summary>The branch's name (admin-written), as the template had it.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The stage it was added after.</summary>
    public Guid AfterStageId { get; init; }

    /// <summary>The stages it added, in order.</summary>
    public List<Guid> StageIds { get; init; } = [];

    /// <summary>Why it was added (free text, at most 500 characters).</summary>
    public string Reason { get; set; } = string.Empty;

    public Guid AddedByUserId { get; init; }
    public DateTime AddedAtUtc { get; init; }
}
