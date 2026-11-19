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
/// One stage of a reviewed template (<c>docs/pathways/dermatology.yaml</c>), with what the model holds: its kind, visit,
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
/// Dermatology's templates, as the light demo has them (<c>MedApp.Web/src/demo/dermatology.ts</c>): the owner's ten
/// reviewed templates (<c>docs/pathways/dermatology.yaml</c>, copied below field for field) and the tour's short relay,
/// "Skin sample to the lab", mapped onto the model the same way:
/// <list type="bullet">
/// <item>An assessment or a lab result is a choice node (<see cref="StageChoiceKind"/>), its outcomes the outcomes; a lab
/// stage is the lab's (<see cref="PathwayTemplateMap.OwnerTeamId"/>); the visit, the letter (a final letter to the GP is a
/// discharge summary, one to the hospital an onward referral, else a clinic letter), the consent and the SNOMED CT code
/// go to the stage definition; a lab result waits for results.</item>
/// <item>Routes only go forward: a link back is left out. An outcome looping to its own stage is dropped (the stage
/// stays current while it repeats); one going back to take a sample again becomes a repeatable addable route
/// ("Repeat biopsy") after the lab's stage; any other ends the route there, still recorded.</item>
/// <item>Stage definitions are shared by name: the first stage of each name decides its definition (the library's
/// "Agree care plan" keeps its own), with the shared descriptions below. A template stage's own notes have no field in
/// the model (the light demo writes them into the stage's description), so the definition's description shows.</item>
/// <item>The first stage stops the waiting-time clock.</item>
/// </list>
/// </summary>
public static class DemoDermatology
{
    /// <summary>The library's own definition the reviewed templates share (as the light demo's <c>LIBRARY</c>).</summary>
    public const string CarePlan = "Agree care plan";

    /// <summary>The tour's relay template (the owner, 19 November 2026).</summary>
    public const string SkinSampleToTheLab = "Skin sample to the lab";

    private static readonly Dictionary<string, string> SharedDescriptions = new()
    {
        ["Triage"] = "Record the referral, the history and what has been tried, and decide when to see the patient.",
        ["Dermoscopy assessment"] = "Skin check and dermoscopy of the lesion, with photographs; record its size and site.",
        ["Take biopsy"] = "Take a skin biopsy for the lab, with the patient’s consent; record the site.",
        ["Histology"] = "The lab processes the sample and enters the histology report.",
        ["Histology result"] = "The clinician records the histology diagnosis.",
        ["Skin cancer MDT"] = "Local skin cancer MDT (dermatology and pathology). Record the team’s decision.",
        ["Final letter to GP"] = "Write to the GP with the diagnosis and care plan, as the centre’s care ends.",
        ["Refer to GP"] = "Refer to the GP for prescribing or ongoing care (the centre doesn’t prescribe).",
        ["Refer to hospital"] = "Refer to the hospital for specialist care or treatment.",
        ["Urgent hospital referral"] = "Urgent referral to the hospital; phone the team first when it’s needed the same day.",
        ["Refer to psychology"] = "Refer for psychological support.",
        ["Take bloods"] = "Take blood for the tests the clinician lists.",
        ["Blood tests"] = "The lab runs the requested tests and enters the results.",
        ["Blood results"] = "The clinician reads the results and records the outcome.",
        ["Take skin sample"] = "Take scrapings, clippings or a swab for the lab; record the sample type and site.",
        ["Microbiology"] = "Microscopy first, then culture; fungal culture can take 2 to 4 weeks.",
        ["Microbiology result"] = "The clinician reads the report and records the outcome.",
    };

    /// <summary>The tour's relay: Triage, the lab's Testing, a Results appointment, then a referral to the GP for medication.</summary>
    private static readonly ReviewedTemplate TourRelay = new(
        SkinSampleToTheLab,
        RetentionClass.Standard,
        "triage",
        [
            new("triage", "Triage", ReviewedKind.Step, VisitRequirement.None, ["testing"], null, "Triage the referral. Where a skin scraping is needed, see the patient and take it for the lab, with their consent.", Snomed: "225390008 | Triage"),
            new("testing", "Testing", ReviewedKind.Step, VisitRequirement.None, ["results"], null, "The lab tests the sample and enters the results.", Lab: true),
            new("results", "Results appointment", ReviewedKind.Step, VisitRequirement.Required, ["refer-gp"], null, "Go through the results with the patient."),
            new("refer-gp", "Refer to GP for medication", ReviewedKind.Step, VisitRequirement.None, [], null, "Refer back to the GP for the medication the results call for (the centre doesn’t prescribe).", NeedsLetter: "Referral letter to GP", Snomed: "306206005 | Referral to service"),
        ],
        []);

    /// <summary>The owner's ten reviewed Dermatology templates, then the tour's relay.</summary>
    public static IReadOnlyList<ReviewedTemplate> All => [.. Reviewed, TourRelay];

    /// <summary>
    /// The stage library: the shared care plan definition, then one per stage name of the templates (their addable
    /// routes' too), in the file's order.
    /// </summary>
    public static IReadOnlyList<DemoDefinition> Definitions()
    {
        var definitions = new List<DemoDefinition>
        {
            new(CarePlan, "Agree a care plan with the patient: advice, self-care and when to come back.", VisitRequirement.Suggested, null, null, null, false, false),
        };
        foreach (var stage in All.SelectMany(t => t.Stages.Concat(t.AddableRoutes.SelectMany(r => r.Stages))))
        {
            if (definitions.Any(d => d.Name == stage.Name))
                continue;
            var snomed = stage.Snomed?.Split('|').Select(part => part.Trim()).ToArray();
            definitions.Add(new DemoDefinition(
                stage.Name,
                SharedDescriptions.GetValueOrDefault(stage.Name) ?? stage.Notes,
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

    /// <summary>Every template mapped onto the model.</summary>
    public static IReadOnlyList<DemoTemplate> Templates() => All.Select(Map).ToList();

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

    /// <summary>The owner's reviewed templates: a copy of <c>docs/pathways/dermatology.yaml</c>, as the light demo's <c>dermatologyReviewed.ts</c>.</summary>
    private static readonly ReviewedTemplate[] Reviewed =
    [
        new(
            "Suspected skin cancer",
            RetentionClass.Oncology,
            "triage",
            [
                new("triage", "Triage", ReviewedKind.Step, VisitRequirement.None, ["dermoscopy"], null, "Urgent suspected cancer referral: record the referral date, lesion site, history and risk factors. Book within the urgent target.", Snomed: "225390008 | Triage"),
                new("dermoscopy", "Dermoscopy assessment", ReviewedKind.Assessment, VisitRequirement.Required, null, [("Looks benign", ["care-plan"]), ("Actinic keratosis", ["refer-gp"]), ("Suspected BCC", ["refer-hospital"]), ("Suspected SCC", ["refer-hospital-urgent"]), ("Suspected melanoma", ["refer-hospital-urgent"]), ("Uncertain: biopsy", ["take-biopsy"])], "Full skin check, dermoscopy and clinical photographs; record lesion size and site."),
                new("take-biopsy", "Take biopsy", ReviewedKind.Step, VisitRequirement.Required, ["histology"], null, "Punch, shave or incisional biopsy. Never a partial biopsy of a possible melanoma: that is excised whole at the hospital.", Snomed: "240977001 | Biopsy of skin", NeedsConsent: true),
                new("histology", "Histology", ReviewedKind.Step, VisitRequirement.None, ["histology-result"], null, "The lab processes the sample and enters the histology report.", Lab: true, Snomed: "252416005 | Histopathology test"),
                new("histology-result", "Histology result", ReviewedKind.LabResult, VisitRequirement.Suggested, null, [("Benign", ["care-plan"]), ("Actinic keratosis", ["refer-gp"]), ("Bowen's disease", ["refer-hospital"]), ("Low-risk BCC", ["refer-hospital"]), ("High-risk BCC", ["mdt"]), ("Squamous cell carcinoma", ["mdt"]), ("Melanoma", ["mdt"]), ("Not diagnostic", ["take-biopsy"])], "The clinician records the histology diagnosis. BCC risk is the clinician's judgement from the report, recorded as given."),
                new("mdt", "Skin cancer MDT", ReviewedKind.Assessment, VisitRequirement.None, null, [("Hospital treatment", ["refer-hospital-urgent"]), ("More tissue needed", ["take-biopsy"]), ("Benign on review", ["care-plan"])], "Local skin cancer MDT (dermatology and pathology). Record the team's decision."),
                new("care-plan", "Agree care plan", ReviewedKind.Step, VisitRequirement.Suggested, ["discharge"], null, "Self-examination and sun protection advice, with written information."),
                new("discharge", "Final letter to GP", ReviewedKind.Step, VisitRequirement.None, [], null, "Histology, the care plan and self-examination advice in the letter.", NeedsLetter: "Final letter to GP", Snomed: "58000006 | Patient discharge"),
                new("refer-gp", "Refer to GP", ReviewedKind.Step, VisitRequirement.None, [], null, "Actinic keratosis: topical treatment needs prescribing. Sun protection advice given; cryotherapy is not offered here.", NeedsLetter: "Referral letter to GP", Snomed: "306206005 | Referral to service"),
                new("refer-hospital", "Refer to hospital", ReviewedKind.Step, VisitRequirement.None, [], null, "Routine referral for treatment: low-risk BCC or Bowen's disease (surgery, cryotherapy or photodynamic therapy).", NeedsLetter: "Referral letter to hospital", Snomed: "306206005 | Referral to service"),
                new("refer-hospital-urgent", "Urgent hospital referral", ReviewedKind.Step, VisitRequirement.None, [], null, "Urgent suspected cancer referral: excision biopsy of a suspected melanoma or SCC, or treatment as the MDT agreed.", NeedsLetter: "Urgent referral letter to hospital", Snomed: "306206005 | Referral to service"),
            ],
            [
                new("Biopsy another lesion", "dermoscopy", [
                    new("biopsy-another", "Take biopsy", ReviewedKind.Step, VisitRequirement.Required, ["histology"], null, "Record the second lesion's site; it goes to the lab as its own sample.", Snomed: "240977001 | Biopsy of skin", NeedsConsent: true),
                ]),
            ]),
        new(
            "Changing mole or lesion check",
            RetentionClass.Standard,
            "triage",
            [
                new("triage", "Triage", ReviewedKind.Step, VisitRequirement.None, ["dermoscopy"], null, "Routine referral: record the reported change, its duration and history.", Snomed: "225390008 | Triage"),
                new("dermoscopy", "Dermoscopy assessment", ReviewedKind.Assessment, VisitRequirement.Required, null, [("Looks benign", ["care-plan"]), ("Uncertain: monitor", ["take-photos"]), ("Suspected skin cancer", ["refer-hospital-urgent"])], "Dermoscopy of the mole and a skin check; record size and site."),
                new("take-photos", "Take photographs", ReviewedKind.Step, VisitRequirement.Required, ["mole-review"], null, "Baseline clinical and dermoscopy photographs, for comparison at review.", NeedsConsent: true),
                new("mole-review", "Mole review", ReviewedKind.Assessment, VisitRequirement.Required, null, [("No change", ["care-plan"]), ("Keep monitoring", ["mole-review"]), ("Mole has changed", ["refer-hospital-urgent"])], "Compare with the earlier photographs; record what changed, if anything."),
                new("care-plan", "Agree care plan", ReviewedKind.Step, VisitRequirement.Suggested, ["discharge"], null, "Self-check advice (what change to look for) and sun protection."),
                new("discharge", "Final letter to GP", ReviewedKind.Step, VisitRequirement.None, [], null, "Diagnosis, care plan and self-care advice in the letter.", NeedsLetter: "Final letter to GP", Snomed: "58000006 | Patient discharge"),
                new("refer-hospital-urgent", "Urgent hospital referral", ReviewedKind.Step, VisitRequirement.None, [], null, "Urgent suspected cancer referral for excision biopsy at the hospital: a possible melanoma is removed whole, not biopsied here.", NeedsLetter: "Urgent referral letter to hospital", Snomed: "306206005 | Referral to service"),
            ],
            []),
        new(
            "Skin cancer surveillance",
            RetentionClass.Oncology,
            "triage",
            [
                new("triage", "Triage", ReviewedKind.Step, VisitRequirement.None, ["skin-check"], null, "Record the hospital's diagnosis, treatment and follow-up plan, including its review interval and end date.", Snomed: "225390008 | Triage"),
                new("skin-check", "Skin check", ReviewedKind.Assessment, VisitRequirement.Required, null, [("No new concerns", ["skin-check"]), ("Plan complete", ["care-plan"]), ("Sun damage found", ["gp-letter"]), ("New lesion: biopsy", ["take-biopsy"]), ("Suspected melanoma", ["refer-hospital-urgent"]), ("Suspected recurrence", ["refer-hospital-urgent"])], "Scar, lymph nodes and a full skin check. Intervals follow the hospital's plan (for example NICE NG14 for melanoma), recorded as given."),
                new("gp-letter", "Letter to GP", ReviewedKind.Step, VisitRequirement.None, ["skin-check"], null, "Actinic keratoses for the GP to treat; surveillance carries on.", NeedsLetter: "Letter to GP"),
                new("take-biopsy", "Take biopsy", ReviewedKind.Step, VisitRequirement.Required, ["histology"], null, "Punch or shave biopsy of a new non-pigmented lesion. A possible melanoma goes to the hospital for excision instead.", Snomed: "240977001 | Biopsy of skin", NeedsConsent: true),
                new("histology", "Histology", ReviewedKind.Step, VisitRequirement.None, ["histology-result"], null, "The lab processes the sample and enters the histology report.", Lab: true, Snomed: "252416005 | Histopathology test"),
                new("histology-result", "Histology result", ReviewedKind.LabResult, VisitRequirement.Suggested, null, [("Benign", ["skin-check"]), ("Actinic keratosis", ["gp-letter"]), ("Low-risk BCC", ["refer-hospital"]), ("High-risk BCC", ["mdt"]), ("Squamous cell carcinoma", ["mdt"]), ("Melanoma", ["mdt"]), ("Not diagnostic", ["take-biopsy"])], "The clinician records the histology diagnosis."),
                new("mdt", "Skin cancer MDT", ReviewedKind.Assessment, VisitRequirement.None, null, [("Hospital treatment", ["refer-hospital-urgent"]), ("More tissue needed", ["take-biopsy"]), ("Benign on review", ["skin-check"])], "Local skin cancer MDT (dermatology and pathology). Record the team's decision."),
                new("care-plan", "Agree care plan", ReviewedKind.Step, VisitRequirement.Suggested, ["discharge"], null, "Self-examination and sun protection advice; how to get back in quickly."),
                new("discharge", "Final letter to GP", ReviewedKind.Step, VisitRequirement.None, [], null, "Surveillance history and self-examination advice in the letter.", NeedsLetter: "Final letter to GP", Snomed: "58000006 | Patient discharge"),
                new("refer-hospital", "Refer to hospital", ReviewedKind.Step, VisitRequirement.None, [], null, "Routine referral for treatment of a new low-risk BCC.", NeedsLetter: "Referral letter to hospital", Snomed: "306206005 | Referral to service"),
                new("refer-hospital-urgent", "Urgent hospital referral", ReviewedKind.Step, VisitRequirement.None, [], null, "Urgent referral back to the treating team: suspected melanoma or recurrence, or treatment as the MDT agreed.", NeedsLetter: "Urgent referral letter to hospital", Snomed: "306206005 | Referral to service"),
            ],
            []),
        new(
            "Inflammatory skin assessment",
            RetentionClass.Standard,
            "triage",
            [
                new("triage", "Triage", ReviewedKind.Step, VisitRequirement.None, ["skin-assessment"], null, "Record symptoms, duration, sites, medicines and allergies as given, and what has been tried.", Snomed: "225390008 | Triage"),
                new("skin-assessment", "Skin assessment", ReviewedKind.Assessment, VisitRequirement.Required, null, [("Self-care is enough", ["care-plan"]), ("Needs prescribed treatment", ["refer-gp"]), ("Blood tests needed", ["take-bloods"]), ("Biopsy needed", ["take-biopsy"]), ("Needs specialist care", ["refer-hospital"]), ("Needs same-day care", ["refer-hospital-urgent"])], "Examine the skin, record distribution and appearance; photographs if useful."),
                new("take-bloods", "Take bloods", ReviewedKind.Step, VisitRequirement.Required, ["blood-tests"], null, "Only tests that help the assessment, listed by the clinician (for itch: full blood count, ferritin, kidney, liver and thyroid tests).", Snomed: "82078001 | Collection of blood specimen for laboratory"),
                new("blood-tests", "Blood tests", ReviewedKind.Step, VisitRequirement.None, ["blood-results"], null, "The lab runs the requested tests and enters the results.", Lab: true),
                new("blood-results", "Blood results", ReviewedKind.LabResult, VisitRequirement.Suggested, null, [("No cause found", ["care-plan"]), ("Cause for GP to treat", ["refer-gp"]), ("Needs specialist care", ["refer-hospital"]), ("Biopsy needed", ["take-biopsy"])], "The clinician reads the results and records the outcome."),
                new("take-biopsy", "Take biopsy", ReviewedKind.Step, VisitRequirement.Required, ["histology"], null, "Punch biopsy for diagnosis; record the site and the question for the pathologist.", Snomed: "240977001 | Biopsy of skin", NeedsConsent: true),
                new("histology", "Histology", ReviewedKind.Step, VisitRequirement.None, ["histology-result"], null, "The lab processes the sample and enters the histology report.", Lab: true, Snomed: "252416005 | Histopathology test"),
                new("histology-result", "Histology result", ReviewedKind.LabResult, VisitRequirement.Suggested, null, [("Self-care is enough", ["care-plan"]), ("Needs prescribed treatment", ["refer-gp"]), ("Needs specialist care", ["refer-hospital"]), ("Unexpected cancer", ["refer-hospital-urgent"]), ("Not diagnostic", ["take-biopsy"])], "The clinician records the histology diagnosis and the outcome."),
                new("care-plan", "Agree care plan", ReviewedKind.Step, VisitRequirement.Suggested, ["discharge"], null, "Advice only, no prescribing: emollient use, skin care, triggers, sun protection."),
                new("discharge", "Final letter to GP", ReviewedKind.Step, VisitRequirement.None, [], null, "Diagnosis, care plan and self-care advice in the letter.", NeedsLetter: "Final letter to GP", Snomed: "58000006 | Patient discharge"),
                new("refer-gp", "Refer to GP", ReviewedKind.Step, VisitRequirement.None, [], null, "Prescribing needed, or a cause found in the bloods for the GP to manage.", NeedsLetter: "Referral letter to GP", Snomed: "306206005 | Referral to service"),
                new("refer-hospital", "Refer to hospital", ReviewedKind.Step, VisitRequirement.None, [], null, "For example patch testing, a suspected blistering or connective tissue disease, phototherapy or systemic treatment.", NeedsLetter: "Referral letter to hospital", Snomed: "306206005 | Referral to service"),
                new("refer-hospital-urgent", "Urgent hospital referral", ReviewedKind.Step, VisitRequirement.None, [], null, "Phone the on-call hospital team the same day; the letter follows. For example widespread blistering or red skin all over; or an unexpected cancer on histology.", NeedsLetter: "Urgent referral letter to hospital", Snomed: "306206005 | Referral to service"),
            ],
            []),
        new(
            "Eczema assessment",
            RetentionClass.Standard,
            "triage",
            [
                new("triage", "Triage", ReviewedKind.Step, VisitRequirement.None, ["eczema-assessment"], null, "Record history, sites, triggers, what has been tried, and allergies as given.", Snomed: "225390008 | Triage"),
                new("eczema-assessment", "Eczema assessment", ReviewedKind.Assessment, VisitRequirement.Required, null, [("Self-care is enough", ["care-plan"]), ("Needs prescribed treatment", ["refer-gp"]), ("Possible contact allergy", ["refer-hospital"]), ("Severe or not controlled", ["refer-hospital"]), ("Needs same-day care", ["refer-hospital-urgent"]), ("Diagnosis unclear", ["take-biopsy"])], "Record sites and appearance; severity measures (EASI, POEM, DLQI) as given, never calculated by MedApp."),
                new("care-plan", "Agree care plan", ReviewedKind.Step, VisitRequirement.Suggested, ["eczema-review"], null, "Advice only, no prescribing: emollient use, skin care, triggers, sun protection. Show how to apply emollients."),
                new("eczema-review", "Eczema review", ReviewedKind.Assessment, VisitRequirement.Required, null, [("Improved", ["discharge"]), ("Needs prescribed treatment", ["refer-gp"]), ("Severe or not controlled", ["refer-hospital"])], "One review about 6 to 8 weeks after the care plan."),
                new("take-biopsy", "Take biopsy", ReviewedKind.Step, VisitRequirement.Required, ["histology"], null, "Punch biopsy where the diagnosis is uncertain (for example to rule out another condition in an adult).", Snomed: "240977001 | Biopsy of skin", NeedsConsent: true),
                new("histology", "Histology", ReviewedKind.Step, VisitRequirement.None, ["histology-result"], null, "The lab processes the sample and enters the histology report.", Lab: true, Snomed: "252416005 | Histopathology test"),
                new("histology-result", "Histology result", ReviewedKind.LabResult, VisitRequirement.Suggested, null, [("Eczema confirmed", ["care-plan"]), ("Other condition", ["refer-hospital"]), ("Not diagnostic", ["take-biopsy"])], "The clinician records the histology diagnosis."),
                new("discharge", "Final letter to GP", ReviewedKind.Step, VisitRequirement.None, [], null, "Diagnosis, care plan and self-care advice in the letter.", NeedsLetter: "Final letter to GP", Snomed: "58000006 | Patient discharge"),
                new("refer-gp", "Refer to GP", ReviewedKind.Step, VisitRequirement.None, [], null, "Prescribing needed (for example topical steroids, or antibiotics for infected eczema).", NeedsLetter: "Referral letter to GP", Snomed: "306206005 | Referral to service"),
                new("refer-hospital", "Refer to hospital", ReviewedKind.Step, VisitRequirement.None, [], null, "Patch testing for a possible contact allergy, or specialist care for severe or uncontrolled eczema (phototherapy, systemic or biologic treatment).", NeedsLetter: "Referral letter to hospital", Snomed: "306206005 | Referral to service"),
                new("refer-hospital-urgent", "Urgent hospital referral", ReviewedKind.Step, VisitRequirement.None, [], null, "Phone the on-call hospital team the same day; the letter follows. For example suspected eczema herpeticum.", NeedsLetter: "Urgent referral letter to hospital", Snomed: "306206005 | Referral to service"),
            ],
            []),
        new(
            "Psoriasis assessment",
            RetentionClass.Standard,
            "triage",
            [
                new("triage", "Triage", ReviewedKind.Step, VisitRequirement.None, ["psoriasis-assessment"], null, "Record history, sites, nails, joints and what has been tried.", Snomed: "225390008 | Triage"),
                new("psoriasis-assessment", "Psoriasis assessment", ReviewedKind.Assessment, VisitRequirement.Required, null, [("Self-care is enough", ["care-plan"]), ("Needs prescribed treatment", ["refer-gp"]), ("Severe or not controlled", ["refer-hospital"]), ("Joint symptoms", ["refer-hospital"]), ("Needs same-day care", ["refer-hospital-urgent"]), ("Diagnosis unclear", ["take-biopsy"])], "Record sites, nails and joint symptoms; PASI, BSA, DLQI and PEST as given, never calculated by MedApp."),
                new("care-plan", "Agree care plan", ReviewedKind.Step, VisitRequirement.Suggested, ["psoriasis-review"], null, "Advice only, no prescribing: emollient use, skin care, triggers, sun protection. Lifestyle advice (smoking, alcohol, weight)."),
                new("psoriasis-review", "Psoriasis review", ReviewedKind.Assessment, VisitRequirement.Required, null, [("Improved", ["discharge"]), ("Needs prescribed treatment", ["refer-gp"]), ("Severe or not controlled", ["refer-hospital"])], "One review about 8 to 12 weeks after the care plan."),
                new("take-biopsy", "Take biopsy", ReviewedKind.Step, VisitRequirement.Required, ["histology"], null, "Punch biopsy where the diagnosis is uncertain.", Snomed: "240977001 | Biopsy of skin", NeedsConsent: true),
                new("histology", "Histology", ReviewedKind.Step, VisitRequirement.None, ["histology-result"], null, "The lab processes the sample and enters the histology report.", Lab: true, Snomed: "252416005 | Histopathology test"),
                new("histology-result", "Histology result", ReviewedKind.LabResult, VisitRequirement.Suggested, null, [("Psoriasis confirmed", ["care-plan"]), ("Other condition", ["refer-hospital"]), ("Not diagnostic", ["take-biopsy"])], "The clinician records the histology diagnosis."),
                new("discharge", "Final letter to GP", ReviewedKind.Step, VisitRequirement.None, [], null, "Diagnosis, care plan and self-care advice in the letter.", NeedsLetter: "Final letter to GP", Snomed: "58000006 | Patient discharge"),
                new("refer-gp", "Refer to GP", ReviewedKind.Step, VisitRequirement.None, [], null, "Prescribing needed (topical treatment); the GP offers the annual review.", NeedsLetter: "Referral letter to GP", Snomed: "306206005 | Referral to service"),
                new("refer-hospital", "Refer to hospital", ReviewedKind.Step, VisitRequirement.None, [], null, "Specialist dermatology for phototherapy or systemic or biologic treatment, or rheumatology for possible psoriatic arthritis.", NeedsLetter: "Referral letter to hospital", Snomed: "306206005 | Referral to service"),
                new("refer-hospital-urgent", "Urgent hospital referral", ReviewedKind.Step, VisitRequirement.None, [], null, "Phone the on-call hospital team the same day; the letter follows. For example red skin all over or widespread pustules.", NeedsLetter: "Urgent referral letter to hospital", Snomed: "306206005 | Referral to service"),
            ],
            []),
        new(
            "Acne assessment",
            RetentionClass.Standard,
            "triage",
            [
                new("triage", "Triage", ReviewedKind.Step, VisitRequirement.None, ["acne-assessment"], null, "Record onset, sites, what has been tried and the effect on daily life.", Snomed: "225390008 | Triage"),
                new("acne-assessment", "Acne assessment", ReviewedKind.Assessment, VisitRequirement.Required, null, [("Self-care is enough", ["care-plan"]), ("Needs prescribed treatment", ["refer-gp"]), ("Severe or scarring", ["refer-hospital"]), ("Needs psychological support", ["refer-psychology"]), ("Needs same-day care", ["refer-hospital-urgent"])], "Record lesion types, sites and scarring; photographs with consent. Hormonal features go to the GP to investigate."),
                new("care-plan", "Agree care plan", ReviewedKind.Step, VisitRequirement.Suggested, ["acne-review"], null, "Skin care advice: gentle cleansing, non-greasy products, no picking. Advice only, no prescribing."),
                new("acne-review", "Acne review", ReviewedKind.Assessment, VisitRequirement.Required, null, [("Improved", ["discharge"]), ("Needs prescribed treatment", ["refer-gp"]), ("Severe or scarring", ["refer-hospital"])], "One review about 12 weeks after the care plan."),
                new("discharge", "Final letter to GP", ReviewedKind.Step, VisitRequirement.None, [], null, "Diagnosis, care plan and self-care advice in the letter.", NeedsLetter: "Final letter to GP", Snomed: "58000006 | Patient discharge"),
                new("refer-gp", "Refer to GP", ReviewedKind.Step, VisitRequirement.None, [], null, "Prescribing needed, or hormonal features for the GP to investigate.", NeedsLetter: "Referral letter to GP", Snomed: "306206005 | Referral to service"),
                new("refer-hospital", "Refer to hospital", ReviewedKind.Step, VisitRequirement.None, [], null, "Consultant-led dermatology: severe acne, scarring, or treatment only they can start.", NeedsLetter: "Referral letter to hospital", Snomed: "306206005 | Referral to service"),
                new("refer-psychology", "Refer to psychology", ReviewedKind.Step, VisitRequirement.None, [], null, "Persistent distress, low mood or body image concerns.", NeedsLetter: "Referral letter to psychology", Snomed: "306206005 | Referral to service"),
                new("refer-hospital-urgent", "Urgent hospital referral", ReviewedKind.Step, VisitRequirement.None, [], null, "Phone the on-call hospital team the same day; the letter follows. For example suspected acne fulminans.", NeedsLetter: "Urgent referral letter to hospital", Snomed: "306206005 | Referral to service"),
            ],
            []),
        new(
            "Suspected skin infection",
            RetentionClass.Standard,
            "triage",
            [
                new("triage", "Triage", ReviewedKind.Step, VisitRequirement.None, ["infection-assessment"], null, "Record symptoms, duration, sites and what has been tried. Spreading redness with fever is for same-day care, not triage.", Snomed: "225390008 | Triage"),
                new("infection-assessment", "Infection assessment", ReviewedKind.Assessment, VisitRequirement.Required, null, [("Sample needed", ["take-sample"]), ("Treat without tests", ["refer-gp"]), ("Not an infection", ["care-plan"]), ("Needs same-day care", ["refer-hospital-urgent"])], "Record appearance, sites and the suspected infection."),
                new("take-sample", "Take skin sample", ReviewedKind.Step, VisitRequirement.Required, ["microbiology"], null, "Skin scrapings or nail clippings for fungus, a swab for bacteria, or a viral swab. Record the sample type and site."),
                new("microbiology", "Microbiology", ReviewedKind.Step, VisitRequirement.None, ["microbiology-result"], null, "Microscopy first, then culture; fungal culture can take 2 to 4 weeks.", Lab: true),
                new("microbiology-result", "Microbiology result", ReviewedKind.LabResult, VisitRequirement.Suggested, null, [("Fungal infection", ["refer-gp"]), ("Bacterial infection", ["refer-gp"]), ("Viral infection", ["refer-gp"]), ("No infection found", ["care-plan"]), ("Sample not adequate", ["take-sample"])], "The clinician reads the report and records the outcome."),
                new("care-plan", "Agree care plan", ReviewedKind.Step, VisitRequirement.Suggested, ["discharge"], null, "Skin care and hygiene advice. Advice only, no prescribing."),
                new("discharge", "Final letter to GP", ReviewedKind.Step, VisitRequirement.None, [], null, "Diagnosis, care plan and self-care advice in the letter.", NeedsLetter: "Final letter to GP", Snomed: "58000006 | Patient discharge"),
                new("refer-gp", "Refer to GP", ReviewedKind.Step, VisitRequirement.None, [], null, "Treatment needs prescribing; include the organism as reported.", NeedsLetter: "Referral letter to GP", Snomed: "306206005 | Referral to service"),
                new("refer-hospital-urgent", "Urgent hospital referral", ReviewedKind.Step, VisitRequirement.None, [], null, "Phone the on-call hospital team the same day; the letter follows. For example spreading cellulitis with fever, or infection near the eye.", NeedsLetter: "Urgent referral letter to hospital", Snomed: "306206005 | Referral to service"),
            ],
            []),
        new(
            "Hair loss assessment",
            RetentionClass.Standard,
            "triage",
            [
                new("triage", "Triage", ReviewedKind.Step, VisitRequirement.None, ["hair-assessment"], null, "Record onset, pattern, recent illness or stress, medicines as given and family history.", Snomed: "225390008 | Triage"),
                new("hair-assessment", "Hair assessment", ReviewedKind.Assessment, VisitRequirement.Required, null, [("Watch and review", ["care-plan"]), ("Blood tests needed", ["take-bloods"]), ("Scalp sample needed", ["take-sample"]), ("Possible scarring loss", ["take-biopsy"]), ("Needs prescribed treatment", ["refer-gp"]), ("Needs psychological support", ["refer-psychology"])], "Examine the scalp and hair (dermoscopy, pull test); scalp photographs with consent."),
                new("take-bloods", "Take bloods", ReviewedKind.Step, VisitRequirement.Required, ["blood-tests"], null, "Only tests that help, listed by the clinician (for example full blood count, ferritin and thyroid function for diffuse shedding).", Snomed: "82078001 | Collection of blood specimen for laboratory"),
                new("blood-tests", "Blood tests", ReviewedKind.Step, VisitRequirement.None, ["blood-results"], null, "The lab runs the requested tests and enters the results.", Lab: true),
                new("blood-results", "Blood results", ReviewedKind.LabResult, VisitRequirement.Suggested, null, [("No cause found", ["care-plan"]), ("Cause for GP to treat", ["refer-gp"]), ("Needs specialist care", ["refer-hospital"])], "The clinician reads the results and records the outcome."),
                new("take-sample", "Take skin sample", ReviewedKind.Step, VisitRequirement.Required, ["microbiology"], null, "Scalp scrapings and plucked hairs for suspected scalp ringworm."),
                new("microbiology", "Microbiology", ReviewedKind.Step, VisitRequirement.None, ["microbiology-result"], null, "Microscopy first, then culture; fungal culture can take 2 to 4 weeks.", Lab: true),
                new("microbiology-result", "Microbiology result", ReviewedKind.LabResult, VisitRequirement.Suggested, null, [("Fungal infection", ["refer-gp"]), ("No infection found", ["care-plan"]), ("Sample not adequate", ["take-sample"])], "The clinician reads the report and records the outcome."),
                new("take-biopsy", "Take biopsy", ReviewedKind.Step, VisitRequirement.Required, ["histology"], null, "Scalp punch biopsy from an active edge; record the sites.", Snomed: "240977001 | Biopsy of skin", NeedsConsent: true),
                new("histology", "Histology", ReviewedKind.Step, VisitRequirement.None, ["histology-result"], null, "The lab processes the sample and enters the histology report.", Lab: true, Snomed: "252416005 | Histopathology test"),
                new("histology-result", "Histology result", ReviewedKind.LabResult, VisitRequirement.Suggested, null, [("Scarring alopecia", ["refer-hospital"]), ("Non-scarring alopecia", ["care-plan"]), ("Not diagnostic", ["take-biopsy"])], "The clinician records the histology diagnosis."),
                new("care-plan", "Agree care plan", ReviewedKind.Step, VisitRequirement.Suggested, ["hair-review"], null, "Advice on what to expect, hair care, wigs and camouflage. Advice only, no prescribing."),
                new("hair-review", "Hair review", ReviewedKind.Assessment, VisitRequirement.Required, null, [("Regrowing or stable", ["discharge"]), ("No change: keep watching", ["hair-review"]), ("Needs prescribed treatment", ["refer-gp"]), ("Needs specialist care", ["refer-hospital"])], "Compare with earlier scalp photographs."),
                new("discharge", "Final letter to GP", ReviewedKind.Step, VisitRequirement.None, [], null, "Diagnosis, care plan and self-care advice in the letter.", NeedsLetter: "Final letter to GP", Snomed: "58000006 | Patient discharge"),
                new("refer-gp", "Refer to GP", ReviewedKind.Step, VisitRequirement.None, [], null, "Prescribing needed, or a cause found in the bloods for the GP to manage.", NeedsLetter: "Referral letter to GP", Snomed: "306206005 | Referral to service"),
                new("refer-hospital", "Refer to hospital", ReviewedKind.Step, VisitRequirement.None, [], null, "Specialist hair clinic: scarring alopecia, or extensive or fast hair loss.", NeedsLetter: "Referral letter to hospital", Snomed: "306206005 | Referral to service"),
                new("refer-psychology", "Refer to psychology", ReviewedKind.Step, VisitRequirement.None, [], null, "Distress about hair loss affecting daily life.", NeedsLetter: "Referral letter to psychology", Snomed: "306206005 | Referral to service"),
            ],
            []),
        new(
            "Skin pigmentation assessment",
            RetentionClass.Standard,
            "triage",
            [
                new("triage", "Triage", ReviewedKind.Step, VisitRequirement.None, ["pigmentation-assessment"], null, "Record onset, spread, triggers and family history. A changing mole goes on its own pathway.", Snomed: "225390008 | Triage"),
                new("pigmentation-assessment", "Pigmentation assessment", ReviewedKind.Assessment, VisitRequirement.Required, null, [("Self-care is enough", ["care-plan"]), ("Blood tests needed", ["take-bloods"]), ("Diagnosis unclear", ["take-biopsy"]), ("Needs prescribed treatment", ["refer-gp"]), ("Needs specialist care", ["refer-hospital"]), ("Needs psychological support", ["refer-psychology"])], "Examine the skin (Wood's lamp if available); photographs with consent."),
                new("take-bloods", "Take bloods", ReviewedKind.Step, VisitRequirement.Required, ["blood-tests"], null, "For example thyroid function in vitiligo, as the clinician lists.", Snomed: "82078001 | Collection of blood specimen for laboratory"),
                new("blood-tests", "Blood tests", ReviewedKind.Step, VisitRequirement.None, ["blood-results"], null, "The lab runs the requested tests and enters the results.", Lab: true),
                new("blood-results", "Blood results", ReviewedKind.LabResult, VisitRequirement.Suggested, null, [("No cause found", ["care-plan"]), ("Cause for GP to treat", ["refer-gp"])], "The clinician reads the results and records the outcome."),
                new("take-biopsy", "Take biopsy", ReviewedKind.Step, VisitRequirement.Required, ["histology"], null, "Punch biopsy where the diagnosis is uncertain.", Snomed: "240977001 | Biopsy of skin", NeedsConsent: true),
                new("histology", "Histology", ReviewedKind.Step, VisitRequirement.None, ["histology-result"], null, "The lab processes the sample and enters the histology report.", Lab: true, Snomed: "252416005 | Histopathology test"),
                new("histology-result", "Histology result", ReviewedKind.LabResult, VisitRequirement.Suggested, null, [("Self-care is enough", ["care-plan"]), ("Needs prescribed treatment", ["refer-gp"]), ("Needs specialist care", ["refer-hospital"]), ("Unexpected cancer", ["refer-hospital-urgent"]), ("Not diagnostic", ["take-biopsy"])], "The clinician records the histology diagnosis and the outcome."),
                new("care-plan", "Agree care plan", ReviewedKind.Step, VisitRequirement.Suggested, ["discharge"], null, "Sun protection, skin camouflage advice and support groups. Advice only, no prescribing."),
                new("discharge", "Final letter to GP", ReviewedKind.Step, VisitRequirement.None, [], null, "Diagnosis, care plan and self-care advice in the letter.", NeedsLetter: "Final letter to GP", Snomed: "58000006 | Patient discharge"),
                new("refer-gp", "Refer to GP", ReviewedKind.Step, VisitRequirement.None, [], null, "Prescribing needed, or a cause found in the bloods for the GP to manage.", NeedsLetter: "Referral letter to GP", Snomed: "306206005 | Referral to service"),
                new("refer-hospital", "Refer to hospital", ReviewedKind.Step, VisitRequirement.None, [], null, "Specialist dermatology, for example phototherapy for vitiligo.", NeedsLetter: "Referral letter to hospital", Snomed: "306206005 | Referral to service"),
                new("refer-psychology", "Refer to psychology", ReviewedKind.Step, VisitRequirement.None, [], null, "Distress about appearance affecting daily life.", NeedsLetter: "Referral letter to psychology", Snomed: "306206005 | Referral to service"),
                new("refer-hospital-urgent", "Urgent hospital referral", ReviewedKind.Step, VisitRequirement.None, [], null, "Urgent suspected cancer referral after an unexpected cancer on histology.", NeedsLetter: "Urgent referral letter to hospital", Snomed: "306206005 | Referral to service"),
            ],
            []),
    ];
}
