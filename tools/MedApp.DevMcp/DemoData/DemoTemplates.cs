using MedApp.Domain.Letters;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.DevMcp.DemoData;

/// <summary>A reviewed stage's kind: a plain step, or a choice node (an assessment, or a lab result back in the clinic).</summary>
public enum ReviewedKind
{
    Step,
    Assessment,
    LabResult,
}

/// <summary>
/// One stage of a reviewed template (a clinic's <c>docs/pathways/*.yaml</c>), with what the model holds: its kind, visit,
/// the stages after it (<paramref name="Next"/>) or a choice node's outcomes, whether the lab works it, the letter it
/// needs (by its own name), its SNOMED CT code (<c>code | term</c>), and the clinician's notes.
/// </summary>
public sealed record ReviewedStage(
    string Id,
    string Name,
    ReviewedKind Kind,
    VisitRequirement Visit,
    string[]? Next,
    (string Name, string[] Next)[]? Outcomes,
    string Notes,
    bool Lab = false,
    string? NeedsLetter = null,
    string? Snomed = null,
    bool Optional = false,
    bool NeedsConsent = false)
{
    /// <summary>The stages straight after it: its outcomes' or its own.</summary>
    public IEnumerable<string> Successors => Outcomes?.SelectMany(o => o.Next) ?? Next ?? [];
}

/// <summary>A reviewed template's addable route: its stages, added after the stage named.</summary>
public sealed record ReviewedRoute(string Name, string AttachesAfter, ReviewedStage[] Stages);

/// <summary>A reviewed template: its name, retention class, first stage, stages and addable routes.</summary>
public sealed record ReviewedTemplate(
    string Name, RetentionClass RetentionClass, string Start, ReviewedStage[] Stages, ReviewedRoute[] AddableRoutes);

/// <summary>A stage definition the seed makes: the stage library's, one per name.</summary>
public sealed record DemoDefinition(
    string Name,
    string Description,
    VisitRequirement Visit,
    string? SnomedCode,
    string? SnomedTerm,
    LetterKind? NeedsLetter,
    bool NeedsConsent,
    bool WaitsForResults);

/// <summary>A template's stage as the model holds it: its place, definition (by name), options, choice and owner.</summary>
public sealed record DemoTemplateStage(
    int StageOrder,
    string Name,
    bool IsOptional,
    StageChoiceKind? ChoiceKind,
    IReadOnlyList<PathwayTemplateOutcome> Outcomes,
    bool IsLabs,
    bool StopsClock);

/// <summary>A template's addable branch as the model holds it: its stages (definitions by name), where it attaches, and whether it repeats.</summary>
public sealed record DemoTemplateBranch(string Name, IReadOnlyList<string> StageNames, IReadOnlyList<int> AttachAfterStageOrders, bool IsRepeatable);

/// <summary>A template mapped onto the model: its stages in order, links (by order) and addable branches.</summary>
public sealed record DemoTemplate(
    string Name,
    RetentionClass RetentionClass,
    IReadOnlyList<DemoTemplateStage> Stages,
    IReadOnlyList<PathwayTemplateLink> Links,
    IReadOnlyList<DemoTemplateBranch> Branches);

/// <summary>
/// A clinic's reviewed templates (<c>docs/pathways/&lt;clinic&gt;.yaml</c>, copied field for field): the team they belong to,
/// the templates in the file's order, and the library's descriptions for the stages several of its templates share.
/// </summary>
public sealed record ReviewedClinic(string Team, IReadOnlyList<ReviewedTemplate> Templates, IReadOnlyDictionary<string, string> SharedDescriptions);

/// <summary>
/// The clinics' reviewed templates mapped onto the model, as the light demo maps them (<c>MedApp.Web/src/demo/clinicTemplates.ts</c>);
/// every clinic's go through this same code (<see cref="Clinics"/>: Dermatology, then the Eye clinic):
/// <list type="bullet">
/// <item>An assessment or a lab result is a choice node (<see cref="StageChoiceKind"/>), its outcomes the outcomes; a lab
/// stage is the lab's (<see cref="PathwayTemplateMap.OwnerTeamId"/>); the visit, the letter (a final letter to the GP is a
/// discharge summary, one to the hospital an onward referral, else a clinic letter), the consent and the SNOMED CT code
/// go to the stage definition; a lab result waits for results.</item>
/// <item>Routes only go forward: a link back is left out. An outcome looping to its own stage is dropped (the stage
/// stays current while it repeats); one going back to take a sample again becomes a repeatable addable route
/// ("Repeat biopsy") after the lab's stage; any other ends the route there, still recorded.</item>
/// <item>Stage definitions are shared by name, across clinics: the first stage of each name decides its definition (the
/// library's "Agree care plan" keeps its own), with its clinic's shared descriptions. A template stage's own notes have
/// no field in the model (the light demo writes them into the stage's description), so the definition's description
/// shows. The end outcome has no field either, so it isn't copied.</item>
/// <item>The first stage stops the waiting-time clock.</item>
/// </list>
/// </summary>
public static class DemoTemplates
{
    /// <summary>The library's own definition the reviewed templates share (as the light demo's <c>LIBRARY</c>).</summary>
    public const string CarePlan = "Agree care plan";

    /// <summary>Every clinic with reviewed templates, in the order the stage library takes their stages.</summary>
    public static IReadOnlyList<ReviewedClinic> Clinics => [DemoDermatology.Clinic, DemoOphthalmology.Clinic];

    /// <summary>
    /// The stage library: the shared care plan definition, then one per stage name of every clinic's templates (their
    /// addable routes' too), clinic by clinic in the files' order. A name a clinic shares with one before it keeps the
    /// earlier definition.
    /// </summary>
    public static IReadOnlyList<DemoDefinition> Definitions() => Definitions(Clinics);

    /// <summary>The stage library made from these clinics' templates (see <see cref="Definitions()"/>).</summary>
    public static IReadOnlyList<DemoDefinition> Definitions(IEnumerable<ReviewedClinic> clinics)
    {
        var definitions = new List<DemoDefinition>
        {
            new(CarePlan, "Agree a care plan with the patient: advice, self-care and when to come back.", VisitRequirement.Suggested, null, null, null, false, false),
        };
        foreach (var (clinic, stage) in clinics.SelectMany(c => c.Templates
                     .SelectMany(t => t.Stages.Concat(t.AddableRoutes.SelectMany(r => r.Stages)))
                     .Select(s => (Clinic: c, Stage: s))))
        {
            if (definitions.Any(d => d.Name == stage.Name))
                continue;
            var snomed = stage.Snomed?.Split('|').Select(part => part.Trim()).ToArray();
            definitions.Add(new DemoDefinition(
                stage.Name,
                clinic.SharedDescriptions.GetValueOrDefault(stage.Name) ?? stage.Notes,
                stage.Visit,
                snomed?[0],
                snomed?[1],
                LetterKindOf(stage.NeedsLetter),
                stage.NeedsConsent,
                stage.Kind == ReviewedKind.LabResult));
        }

        return definitions;
    }

    /// <summary>The letter kind a stage's letter is: a final letter to the GP, an onward referral to the hospital, or a clinic letter.</summary>
    public static LetterKind? LetterKindOf(string? letter) =>
        letter is null ? null
        : letter.StartsWith("Final letter", StringComparison.OrdinalIgnoreCase) ? LetterKind.DischargeSummary
        : letter.EndsWith("to hospital", StringComparison.OrdinalIgnoreCase) ? LetterKind.OnwardReferral
        : LetterKind.ClinicLetter;

    /// <summary>A clinic's templates mapped onto the model, in its file's order.</summary>
    public static IReadOnlyList<DemoTemplate> Templates(ReviewedClinic clinic) => clinic.Templates.Select(Map).ToList();

    /// <summary>
    /// The template's stages in an order where every route goes forward: a depth-first walk from the start marks the
    /// links back (to a stage still on the walk), and the rest are sorted, keeping the file's order where they allow it.
    /// </summary>
    public static (List<ReviewedStage> Order, Func<string, string, bool> IsBack) ForwardOrder(ReviewedTemplate template)
    {
        var byId = template.Stages.ToDictionary(s => s.Id);
        var back = new HashSet<(string, string)>();
        var seen = new HashSet<string>();
        var onWalk = new HashSet<string>();

        void Walk(string id)
        {
            seen.Add(id);
            onWalk.Add(id);
            foreach (var next in byId[id].Successors)
            {
                if (onWalk.Contains(next))
                    back.Add((id, next));
                else if (!seen.Contains(next))
                    Walk(next);
            }

            onWalk.Remove(id);
        }

        Walk(template.Start);
        bool IsBack(string from, string to) => back.Contains((from, to));

        var incoming = template.Stages.ToDictionary(s => s.Id, _ => 0);
        foreach (var stage in template.Stages)
            foreach (var next in stage.Successors.Distinct())
                if (!IsBack(stage.Id, next))
                    incoming[next]++;

        var order = new List<ReviewedStage>();
        var left = template.Stages.ToList();
        while (left.Count > 0)
        {
            var index = Math.Max(0, left.FindIndex(s => incoming[s.Id] == 0));
            var stage = left[index];
            left.RemoveAt(index);
            order.Add(stage);
            foreach (var next in stage.Successors.Distinct())
                if (!IsBack(stage.Id, next))
                    incoming[next]--;
        }

        return (order, IsBack);
    }

    /// <summary>One reviewed template mapped onto the model (the light demo's <c>toTemplate</c>).</summary>
    public static DemoTemplate Map(ReviewedTemplate template)
    {
        var (order, isBack) = ForwardOrder(template);
        var orderOf = order.Select((s, i) => (s.Id, Order: i + 1)).ToDictionary(x => x.Id, x => x.Order);
        var byId = template.Stages.ToDictionary(s => s.Id);
        var links = new List<PathwayTemplateLink>();

        void Link(string from, string to)
        {
            var link = new PathwayTemplateLink(orderOf[from], orderOf[to]);
            if (!links.Contains(link))
                links.Add(link);
        }

        // A route back to take a sample again: the sampling stage and the lab's stage after it, added after the lab's.
        bool IsSampling(ReviewedStage stage) => (stage.Next ?? []).Any(n => byId.TryGetValue(n, out var s) && s.Lab);
        var repeatRoutes = new List<DemoTemplateBranch>();
        void RepeatRoute(ReviewedStage sampling)
        {
            var lab = byId[sampling.Next!.First(n => byId[n].Lab)];
            var taken = sampling.Name.StartsWith("Take ", StringComparison.Ordinal) ? sampling.Name[5..] : sampling.Name;
            var name = $"Repeat {taken.ToLowerInvariant()}";
            if (repeatRoutes.All(r => r.Name != name))
                repeatRoutes.Add(new DemoTemplateBranch(name, [sampling.Name, lab.Name], [orderOf[lab.Id]], true));
        }

        var stages = new List<DemoTemplateStage>();
        for (var i = 0; i < order.Count; i++)
        {
            var stage = order[i];
            var outcomes = new List<PathwayTemplateOutcome>();
            if (stage.Outcomes is not null)
            {
                foreach (var (name, next) in stage.Outcomes)
                {
                    var ahead = next.Where(n => !isBack(stage.Id, n)).ToList();
                    foreach (var n in ahead)
                        Link(stage.Id, n);
                    if (ahead.Count > 0 || next.Length == 0)
                    {
                        outcomes.Add(new PathwayTemplateOutcome(name, ahead.Select(n => orderOf[n]).Order().ToList()));
                        continue;
                    }

                    var target = byId[next[0]];
                    if (target.Id == stage.Id)
                        continue; // The stage stays current while it repeats.
                    if (IsSampling(target))
                        RepeatRoute(target);
                    else
                        outcomes.Add(new PathwayTemplateOutcome(name, []));
                }
            }
            else
            {
                foreach (var next in stage.Next ?? [])
                    if (!isBack(stage.Id, next))
                        Link(stage.Id, next);
            }

            var choice = stage.Kind switch
            {
                ReviewedKind.Assessment => StageChoiceKind.Assessment,
                ReviewedKind.LabResult => StageChoiceKind.LabResult,
                _ => (StageChoiceKind?)null,
            };
            stages.Add(new DemoTemplateStage(i + 1, stage.Name, stage.Optional, choice, choice is null ? [] : outcomes, stage.Lab, StopsClock: i == 0));
        }

        // The reviewed file's addable routes: their stages, then the lab's stage they lead into (a biopsy's histology).
        var reviewedRoutes = template.AddableRoutes.Select(route =>
        {
            var into = (route.Stages[^1].Next ?? []).Select(n => byId.GetValueOrDefault(n)).OfType<ReviewedStage>().Where(s => s.Lab);
            return new DemoTemplateBranch(route.Name, route.Stages.Concat(into).Select(s => s.Name).ToList(), [orderOf[route.AttachesAfter]], false);
        });

        return new DemoTemplate(
            template.Name,
            template.RetentionClass,
            stages,
            links.OrderBy(l => l.FromStageOrder).ThenBy(l => l.ToStageOrder).ToList(),
            [.. reviewedRoutes, .. repeatRoutes]);
    }
}
